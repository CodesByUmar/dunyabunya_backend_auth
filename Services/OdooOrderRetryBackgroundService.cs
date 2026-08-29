using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

/// <summary>
/// Marketplace'da yaratilgan buyurtmalarni Odoo'da sale.order sifatida yaratib
/// qo'yadi (OdooSaleOrderId hali null bo'lgan buyurtmalar uchun) — mijoz Odoo'ga
/// hali sinxronlanmagan bo'lsa (OdooPartnerId=null) yoki Odoo vaqtincha ishlamay
/// qolsa, keyingi davrda avtomatik qayta uriniladi.
/// </summary>
public class OdooOrderRetryBackgroundService : BackgroundService
{
    private const int BatchSize = 20;

    private readonly IServiceProvider _services;
    private readonly IOdooOrderSyncQueue _queue;
    private readonly ILogger<OdooOrderRetryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public OdooOrderRetryBackgroundService(IServiceProvider services, IOdooOrderSyncQueue queue, IConfiguration config, ILogger<OdooOrderRetryBackgroundService> logger)
    {
        _services = services;
        _queue = queue;
        _logger = logger;
        var minutes = double.TryParse(config["Odoo:RetryIntervalMinutes"], out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(
            ProcessQueueAsync(stoppingToken),
            RunPeriodicScanAsync(stoppingToken));
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var orderId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var odoo = scope.ServiceProvider.GetRequiredService<IOdooService>();

                    var order = await db.Orders.Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Id == orderId, stoppingToken);
                    if (order == null || order.OdooSaleOrderId != null) continue;

                    await SyncOneOrderAsync(db, odoo, order, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Odoo buyurtma navbati: order {OrderId} uchun sinxronizatsiya muvaffaqiyatsiz bo'ldi.", orderId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ilova to'xtayotganda normal holat.
        }
    }

    private async Task RunPeriodicScanAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryUnsyncedAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odoo buyurtma retry background job'da kutilmagan xato.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Ilova to'xtayotganda normal holat.
            }
        }
    }

    private async Task RetryUnsyncedAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var odoo = scope.ServiceProvider.GetRequiredService<IOdooService>();

        var unsynced = await db.Orders.Include(o => o.Items)
            .Where(o => o.OdooSaleOrderId == null)
            .OrderBy(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (unsynced.Count == 0) return;

        var syncedCount = 0;
        foreach (var order in unsynced)
        {
            if (await SyncOneOrderAsync(db, odoo, order, ct)) syncedCount++;
        }

        if (unsynced.Count > 0)
        {
            _logger.LogInformation("Odoo buyurtma retry: {Synced}/{Total} muvaffaqiyatli sinxronlandi.", syncedCount, unsynced.Count);
        }
    }

    private async Task<bool> SyncOneOrderAsync(AppDbContext db, IOdooService odoo, Order order, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == order.UserId, ct);
        if (user?.OdooPartnerId == null)
        {
            // Mijoz hali Odoo'ga sinxronlanmagan — keyingi davrda qayta uriniladi
            // (user sinxronlangach, bu buyurtma ham o'tadi).
            return false;
        }

        var lines = order.Items
            .Where(i => i.OdooProductId != null)
            .Select(i => (OdooProductId: i.OdooProductId!.Value, Quantity: i.Quantity, PriceUnit: i.Price))
            .ToList();

        if (lines.Count == 0)
        {
            _logger.LogWarning("Order {OrderId}: hech qanday qatorda OdooProductId yo'q — Odoo'ga yuborilmadi.", order.Id);
            return false;
        }

        try
        {
            var saleOrderId = await odoo.CreateSaleOrderAsync(user.OdooPartnerId.Value, $"Marketplace #{order.Id}", lines);
            order.OdooSaleOrderId = saleOrderId;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order {OrderId} uchun Odoo sale.order yaratib bo'lmadi.", order.Id);
            return false;
        }
    }
}
