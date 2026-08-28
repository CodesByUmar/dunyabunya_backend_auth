using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Manzil matnini (masalan mijoz qo'lda yozgan) Yandex Geocoder HTTP API orqali
/// koordinataga aylantiradi — mijoz xaritadan tanlamasdan, faqat matn yozganda ham
/// buyurtmada Lat/Lng bo'sh qolmasligi uchun. Bepul kunlik limit bor (Yandex Cabinet),
/// shuning uchun xato/limit tugashi buyurtma yaratilishini bloklamasligi kerak —
/// har doim "aniqlanmadi" holatini yumshoq qaytaradi.
/// </summary>
public class YandexGeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<YandexGeocodingService> _logger;

    public YandexGeocodingService(HttpClient http, IConfiguration config, ILogger<YandexGeocodingService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<(double Lat, double Lng)?> GeocodeAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var apiKey = _config["Yandex:GeocoderApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Yandex:GeocoderApiKey sozlanmagan — geokodlash o'tkazib yuborildi.");
            return null;
        }

        try
        {
            var url = $"https://geocode-maps.yandex.ru/1.x/?apikey={Uri.EscapeDataString(apiKey)}" +
                       $"&geocode={Uri.EscapeDataString(address)}&format=json&results=1&lang=ru_RU";

            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yandex Geocoder xato qaytardi: {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var members = doc.RootElement
                .GetProperty("response")
                .GetProperty("GeoObjectCollection")
                .GetProperty("featureMember");

            if (members.GetArrayLength() == 0) return null;

            // Yandex "pos" ni "long lat" tartibida qaytaradi (lat emas!).
            var pos = members[0]
                .GetProperty("GeoObject")
                .GetProperty("Point")
                .GetProperty("pos")
                .GetString();

            if (string.IsNullOrEmpty(pos)) return null;

            var parts = pos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return null;
            if (!double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var lng)) return null;
            if (!double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var lat)) return null;

            return (lat, lng);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yandex Geocoder so'rovi muvaffaqiyatsiz bo'ldi.");
            return null;
        }
    }
}
