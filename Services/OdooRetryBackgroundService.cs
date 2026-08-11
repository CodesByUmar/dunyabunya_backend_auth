using AuthApi.Data;
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
    private readonly ILogger<OdooRetryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public OdooRetryBackgroundService(IServiceProvider services, IConfiguration config, ILogger<OdooRetryBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
        var minutes = double.TryParse(config["Odoo:RetryIntervalMinutes"], out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
            try
            {
                var partnerId = await odoo.GetOrCreatePartnerAsync($"{user.FirstName} {user.LastName}", user.PhoneNumber, user.Email);
                if (partnerId.HasValue)
                {
                    user.OdooPartnerId = partnerId;
                    syncedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odoo retry: user {UserId} uchun yana muvaffaqiyatsiz bo'ldi.", user.Id);
            }
        }

        if (syncedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Odoo retry: {Synced}/{Total} muvaffaqiyatli sinxronlandi.", syncedCount, unsynced.Count);
    }
}
