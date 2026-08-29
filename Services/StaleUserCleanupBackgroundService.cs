using AuthApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

// Ro'yxatdan o'tishni oxirigacha yakunlamagan (PasswordHash hali o'rnatilmagan —
// AuthController.CompleteRegistration shu maydonni yakuniy bosqichda to'ldiradi)
// va 1 kundan beri shu holatda turgan userlarni bazadan avtomatik o'chiradi.
// Bunday qatorlar odatda email kod bilan tasdiqlangan-u, telefon/parol
// bosqichini tashlab ketilgan "chala" ro'yxatdan o'tishlar.
public class StaleUserCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StaleUserCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(1);

    public StaleUserCleanupBackgroundService(IServiceProvider services, ILogger<StaleUserCleanupBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chala ro'yxatdan o'tgan userlarni tozalashda kutilmagan xato.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Ilova to'xtayotganda normal holat.
            }
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - StaleAfter;
        var deleted = await db.Users
            .Where(u => u.PasswordHash == null && u.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Chala ro'yxatdan o'tish tozalandi: {Deleted} ta user (1 kundan ortiq parol o'rnatilmagan holda turgan) o'chirildi.",
                deleted);
        }
    }
}
