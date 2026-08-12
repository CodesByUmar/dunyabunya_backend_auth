using System.Net.Http.Json;
using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Odoo bilan standart JSON-RPC (/jsonrpc) orqali gaplashadi — maxsus Odoo modul
/// talab qilinmaydi, har qanday Odoo instansida tayyor mavjud (common.authenticate,
/// object.execute_kw). Telefon raqami orqali res.partner'ni qidiradi/yaratadi.
/// </summary>
public class OdooService : IOdooService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OdooService> _logger;

    public OdooService(HttpClient http, IConfiguration config, ILogger<OdooService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<int?> GetOrCreatePartnerAsync(string fullName, string phone, string email)
    {
        // Telefonsiz Odoo'da mijozni topib/yaratib bo'lmaydi — bog'lash faqat shu orqali.
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var db = _config["Odoo:Database"];
        var username = _config["Odoo:Username"];
        var apiKey = _config["Odoo:ApiKeyOutbound"];

        if (string.IsNullOrEmpty(db) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Odoo sozlamalari to'liq emas (Database/Username/ApiKeyOutbound) — sinxronizatsiya o'tkazib yuborildi.");
            return null;
        }

        var uid = await AuthenticateAsync(db, username, apiKey);

        var existingId = await SearchPartnerByPhoneAsync(db, uid, apiKey, phone);
        var partnerId = existingId ?? await CreatePartnerAsync(db, uid, apiKey, fullName, phone, email);

        // Mavjud bo'lsin, yangi yaratilgan bo'lsin — marketplace orqali kelgan
        // mijoz sifatida belgilaymiz (Odoo Contacts'da filtrlash uchun tag).
        // Bu xato bersa ham asosiy oqim (partnerId) buzilmasligi kerak.
        await TagAsMarketplaceAsync(db, uid, apiKey, partnerId);

        return partnerId;
    }

    private async Task<int> AuthenticateAsync(string db, string username, string apiKey)
    {
        var result = await CallAsync("common", "authenticate",
            new object[] { db, username, apiKey, new Dictionary<string, object>() });

        return result.GetInt32();
    }

    private async Task<int?> SearchPartnerByPhoneAsync(string db, int uid, string apiKey, string phone)
    {
        // 1) Avval aniq (=) mos kelishni tekshiramiz — tez yo'l, ko'p yozuvlar shu
        // formatda saqlangan bo'ladi.
        var exactId = await SearchExactAsync(db, uid, apiKey, phone);
        if (exactId.HasValue) return exactId;

        // 2) Odoo'da eski yozuvlar ko'pincha bo'shliq/chiziqcha bilan saqlangan
        // (masalan "+998 90 970 28 58"), shuning uchun tutash substring qidiruv ham
        // bo'shliqqa duch kelib topolmay qolishi mumkin. Shu sababli mahalliy 9 xonali
        // qismning HAR BIR raqami orasiga "%" (wildcard) qo'yib qidiramiz — shunda
        // orada qanday belgi (bo'shliq, chiziqcha) bo'lishidan qat'i nazar topiladi.
        var digitsOnly = OnlyDigits(phone);
        if (digitsOnly.Length < 9) return null;
        var localDigits = digitsOnly[^9..];
        var pattern = "%" + string.Join("%", localDigits.ToCharArray()) + "%";

        var fuzzyDomain = new object[]
        {
            new object[] { "|", new object[] { "phone", "ilike", pattern }, new object[] { "mobile", "ilike", pattern } }
        };
        var fuzzyKwargs = new Dictionary<string, object> { ["fields"] = new[] { "id", "phone", "mobile" }, ["limit"] = 20 };

        var fuzzyResult = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner", "search_read", fuzzyDomain, fuzzyKwargs });

        foreach (var item in fuzzyResult.EnumerateArray())
        {
            var candidatePhone = GetStringOrNull(item, "phone");
            var candidateMobile = GetStringOrNull(item, "mobile");

            if (OnlyDigits(candidatePhone) == digitsOnly || OnlyDigits(candidateMobile) == digitsOnly)
            {
                return item.GetProperty("id").GetInt32();
            }
        }

        return null;
    }

    private async Task<int?> SearchExactAsync(string db, int uid, string apiKey, string phone)
    {
        var domain = new object[]
        {
            new object[] { "|", new object[] { "phone", "=", phone }, new object[] { "mobile", "=", phone } }
        };
        var kwargs = new Dictionary<string, object> { ["fields"] = new[] { "id" }, ["limit"] = 1 };

        var result = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner", "search_read", domain, kwargs });

        foreach (var item in result.EnumerateArray())
        {
            return item.GetProperty("id").GetInt32();
        }

        return null;
    }

    private static string? GetStringOrNull(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string OnlyDigits(string? value) =>
        value == null ? "" : new string(value.Where(char.IsDigit).ToArray());

    // res.partner.title "XARIDORLAR" — Odoo tomonida mijozlarni asosiy tasniflash shu
    // maydon (Title) orqali qilinadi (Tags emas). Yangi mijozlar ham shu bo'limga tushishi
    // uchun yaratishda o'rnatiladi.
    private const int XaridorlarTitleId = 84;

    private async Task<int> CreatePartnerAsync(string db, int uid, string apiKey, string fullName, string phone, string email)
    {
        var values = new Dictionary<string, object?>
        {
            ["name"] = string.IsNullOrWhiteSpace(fullName) ? phone : fullName,
            ["phone"] = phone,
            ["customer_rank"] = 1,
            ["title"] = XaridorlarTitleId
        };
        if (!string.IsNullOrWhiteSpace(email)) values["email"] = email;

        var result = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner", "create", new object[] { values } });

        return result.GetInt32();
    }

    private static int? _marketplaceTagIdCache;

    /// <summary>
    /// Mijozga "Marketplace" tegini qo'yadi — Odoo Contacts'da bu mijoz marketplace
    /// orqali kelganini filtrlab ko'rish uchun. Xato bo'lsa faqat log yoziladi,
    /// asosiy oqimga (partnerId) ta'sir qilmaydi.
    /// </summary>
    private async Task TagAsMarketplaceAsync(string db, int uid, string apiKey, int partnerId)
    {
        // "Marketplace" tegini birinchi marta qidirish/yaratishda (kesh bo'sh bo'lganda)
        // Odoo tomonidan vaqti-vaqti bilan tasodifiy xato ("string index out of range")
        // kelishi kuzatildi — sababi noaniq, lekin qayta urinishda odatda o'tadi.
        // Shuning uchun 2 marta urinamiz, baribir muvaffaqiyatsiz bo'lsa faqat log yoziladi.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var tagId = await GetOrCreateMarketplaceTagAsync(db, uid, apiKey);

                await CallAsync("object", "execute_kw", new object[]
                {
                    db, uid, apiKey, "res.partner", "write",
                    new object[]
                    {
                        new object[] { partnerId },
                        new Dictionary<string, object> { ["category_id"] = new object[] { new object[] { 4, tagId } } }
                    }
                });
                return;
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    _logger.LogWarning(ex, "Partner {PartnerId}ga 'Marketplace' tegini qo'yib bo'lmadi.", partnerId);
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }
    }

    private async Task<int> GetOrCreateMarketplaceTagAsync(string db, int uid, string apiKey)
    {
        if (_marketplaceTagIdCache.HasValue) return _marketplaceTagIdCache.Value;

        var domain = new object[] { new object[] { "name", "=", "Marketplace" } };
        var kwargs = new Dictionary<string, object> { ["fields"] = new[] { "id" }, ["limit"] = 1 };

        var result = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner.category", "search_read", domain, kwargs });

        foreach (var item in result.EnumerateArray())
        {
            _marketplaceTagIdCache = item.GetProperty("id").GetInt32();
            return _marketplaceTagIdCache.Value;
        }

        var createResult = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "res.partner.category", "create",
            new object[] { new Dictionary<string, object> { ["name"] = "Marketplace" } }
        });

        _marketplaceTagIdCache = createResult.GetInt32();
        return _marketplaceTagIdCache.Value;
    }

    private async Task<JsonElement> CallAsync(string service, string method, object[] args)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "call",
            ["params"] = new Dictionary<string, object?>
            {
                ["service"] = service,
                ["method"] = method,
                ["args"] = args
            },
            ["id"] = Random.Shared.Next()
        };

        using var response = await _http.PostAsJsonAsync("jsonrpc", payload);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("data", out var data) && data.TryGetProperty("message", out var m)
                ? m.GetString()
                : error.TryGetProperty("message", out var m2) ? m2.GetString() : "Noma'lum Odoo xatosi";

            throw new InvalidOperationException($"Odoo xatosi ({service}.{method}): {message}");
        }

        return root.GetProperty("result").Clone();
    }
}
