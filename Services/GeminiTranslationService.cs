using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Matnni (kategoriya nomi, mahsulot tavsifi, xususiyat va h.k.) Gemini API orqali
/// TAKLIF sifatida tarjima qiladi — RU->UZ yoki UZ->RU, chaqiruvchi belgilagan
/// yo'nalishda. Admin buni ko'rib, xohlasa tuzatib, keyin saqlaydi (avtomatik,
/// ko'rib chiqmasdan nashr qilinmaydi). Xato/limit tugashi so'rovni bloklamasligi
/// kerak — har doim yumshoq (null) qaytadi, chaqiruvchi taklifsiz davom eta oladi.
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

    public async Task<string?> SuggestTranslationAsync(string text, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var normalizedTarget = targetLang.Trim().ToLowerInvariant();
        if (normalizedTarget != "ru" && normalizedTarget != "uz")
        {
            _logger.LogWarning("Noto'g'ri targetLang: {TargetLang} — faqat 'ru' yoki 'uz' bo'lishi mumkin.", targetLang);
            return null;
        }

        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey sozlanmagan — tarjima taklifi o'tkazib yuborildi.");
            return null;
        }

        try
        {
            var targetLangName = normalizedTarget == "uz" ? "o'zbek tiliga (lotin yozuvida)" : "rus tiliga";

            var prompt =
                "Siz qurilish-ta'mirlash mollari onlayn-do'koni uchun professional tarjimonsiz. " +
                $"Quyidagi matnni {targetLangName} tarjima qiling (manba tili rus yoki o'zbek bo'lishi mumkin — o'zingiz aniqlang). " +
                "Faqat tarjima natijasini yozing — hech qanday izoh, tirnoq belgisi yoki qo'shimcha so'z qo'shmang.\n\n" +
                $"Matn: {text}";

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

            var resultText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return resultText?.Trim().Trim('"');
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini tarjima so'rovi muvaffaqiyatsiz bo'ldi.");
            return null;
        }
    }
}
