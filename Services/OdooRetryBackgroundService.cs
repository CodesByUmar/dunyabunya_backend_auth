using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

/// <summary>
/// Odoo bilan sinxronizatsiyasi muvaffaqiyatsiz bo'lgan (OdooPartnerId = null)
/// foydalanuvchilarni davriy ravishda qayta urinib sinxronlaydi — Odoo vaqtincha
/// ishlamay qolgan bo'lsa, keyinroq avtomatik tuzatiladi, qo'lda aralashuv shart emas.
/// </summary>
public class OdooRetryBackgroundService : BackgroundService
{
    private const int BatchSize = 20;

    private readonly IServiceProvider _services;
    private readonly IOdooSyncQueue _queue;
    private readonly ILogger<OdooRetryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public OdooRetryBackgroundService(IServiceProvider services, IOdooSyncQueue queue, IConfiguration config, ILogger<OdooRetryBackgroundService> logger)
    {
        _services = services;
        _queue = queue;
        _logger = logger;
        var minutes = double.TryParse(config["Odoo:RetryIntervalMinutes"], out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ikki parallel oqim: 1) navbatga tushgan userni deyarli darhol sinxronlaydi
        // (registratsiyadan keyin tez javob kerak bo'lgani uchun), 2) davriy skanerlash —
        // navbatdan o'tib ketgan yoki oldingi urinishda muvaffaqiyatsiz bo'lganlar uchun
        // zaxira mexanizm.
        return Task.WhenAll(
            ProcessQueueAsync(stoppingToken),
            RunPeriodicScanAsync(stoppingToken));
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var userId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var odoo = scope.ServiceProvider.GetRequiredService<IOdooService>();

                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, stoppingToken);
                    if (user == null || user.OdooPartnerId != null) continue;

                    await SyncOneUserAsync(db, odoo, user, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Odoo navbat: user {UserId} uchun sinxronizatsiya muvaffaqiyatsiz bo'ldi.", userId);
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
                _logger.LogWarning(ex, "Odoo retry background job'da kutilmagan xato.");
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

        // Telefonsiz userlarni qayta urinib ko'rishning ma'nosi yo'q — OdooService
        // ular uchun har doim null qaytaradi.
        var unsynced = await db.Users
            .Where(u => u.OdooPartnerId == null && u.PhoneNumber != "")
            .OrderBy(u => u.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (unsynced.Count == 0) return;

        _logger.LogInformation("Odoo retry: {Count} ta sinxronlanmagan foydalanuvchi topildi.", unsynced.Count);

        var syncedCount = 0;
        foreach (var user in unsynced)
        {
            if (await SyncOneUserAsync(db, odoo, user, ct)) syncedCount++;
        }

        _logger.LogInformation("Odoo retry: {Synced}/{Total} muvaffaqiyatli sinxronlandi.", syncedCount, unsynced.Count);
    }

    /// <summary>Bitta userni Odoo bilan sinxronlaydi va muvaffaqiyatli bo'lsa DB'ga saqlaydi.</summary>
    private async Task<bool> SyncOneUserAsync(AppDbContext db, IOdooService odoo, User user, CancellationToken ct)
    {
        try
        {
            var partnerId = await odoo.GetOrCreatePartnerAsync($"{user.FirstName} {user.LastName}", user.PhoneNumber, user.Email);
            if (!partnerId.HasValue) return false;

            user.OdooPartnerId = partnerId;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Odoo sinxronizatsiya: user {UserId} uchun muvaffaqiyatsiz bo'ldi.", user.Id);
            return false;
        }
    }
}
