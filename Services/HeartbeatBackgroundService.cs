namespace AuthApi.Services;

/// <summary>
/// Xizmat ishlab turganini healthchecks.io'ga davriy "men tirikman" signali
/// yuborish orqali bildiradi. Agar signal to'xtasa (server o'chib qolsa,
/// dastur qulasa), healthchecks.io Telegram orqali avtomatik ogohlantiradi.
/// Healthchecks:PingUrl bo'sh bo'lsa, bu xizmat sokin o'zini o'chiradi.
/// </summary>
public class HeartbeatBackgroundService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HeartbeatBackgroundService> _logger;
    private readonly string? _pingUrl;
    private readonly TimeSpan _interval;

    public HeartbeatBackgroundService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<HeartbeatBackgroundService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _pingUrl = config["Healthchecks:PingUrl"];
        var minutes = double.TryParse(config["Healthchecks:IntervalMinutes"], out var m) ? m : 10;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_pingUrl))
        {
            _logger.LogInformation("Healthchecks:PingUrl sozlanmagan — heartbeat xizmati o'chirilgan.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await client.GetAsync(_pingUrl, stoppingToken);
            }
            catch (Exception ex)
            {
                // Internet vaqtincha yo'q bo'lsa ham dastur ishlashda davom etadi —
                // shunchaki keyingi davrda qayta urinadi. healthchecks.io o'zi
                // signal kelmasa ogohlantiradi.
                _logger.LogWarning(ex, "Heartbeat ping yuborilmadi.");
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
}
