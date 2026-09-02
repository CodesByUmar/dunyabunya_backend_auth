using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Rus tilidagi matnni (kategoriya nomi, mahsulot tavsifi, xususiyat va h.k.)
/// Gemini API orqali o'zbek tiliga TAKLIF sifatida tarjima qiladi — admin buni
/// ko'rib, xohlasa tuzatib, keyin saqlaydi (avtomatik, ko'rib chiqmasdan
/// nashr qilinmaydi). Xato/limit tugashi so'rovni bloklamasligi kerak — har doim
/// yumshoq (null) qaytadi, chaqiruvchi taklifsiz davom eta oladi.
///
/// MUHIM: "gemini-3.6-flash" kabi eng yangi "thinking" modellar qisqa
/// tarjima so'rovlarida ba'zan bo'sh javob qaytaradi (fikrlash tokenlariga
/// butun byudjetni sarflab qo'yadi) — shuning uchun ataylab "gemini-flash-lite-latest"
/// ishlatiladi, u qisqa, aniq javob berishda ishonchli ekani tekshirilgan.
/// </summary>
public class GeminiTranslationService : ITranslationService
{
    private const string Model = "gemini-flash-lite-latest";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiTranslationService> _logger;

    public GeminiTranslationService(HttpClient http, IConfiguration config, ILogger<GeminiTranslationService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string?> SuggestUzTranslationAsync(string ruText)
    {
        if (string.IsNullOrWhiteSpace(ruText)) return null;

        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey sozlanmagan — tarjima taklifi o'tkazib yuborildi.");
            return null;
        }

        try
        {
            var prompt =
                "Siz qurilish-ta'mirlash mollari onlayn-do'koni uchun professional tarjimonsiz. " +
                "Quyidagi rus tilidagi matnni o'zbek tiliga (lotin yozuvida) tarjima qiling. " +
                "Faqat tarjima natijasini yozing — hech qanday izoh, tirnoq belgisi yoki qo'shimcha so'z qo'shmang.\n\n" +
                $"Rus tilidagi matn: {ruText}";

            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { maxOutputTokens = 1000, temperature = 0.2 }
            };

            var url = $"v1beta/models/{Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
            using var response = await _http.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API xato qaytardi: {Status} {Body}", response.StatusCode, body);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text?.Trim().Trim('"');
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini tarjima so'rovi muvaffaqiyatsiz bo'ldi.");
            return null;
        }
    }
}
