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
        if (existingId.HasValue) return existingId;

        return await CreatePartnerAsync(db, uid, apiKey, fullName, phone, email);
    }

    private async Task<int> AuthenticateAsync(string db, string username, string apiKey)
    {
        var result = await CallAsync("common", "authenticate",
            new object[] { db, username, apiKey, new Dictionary<string, object>() });

        return result.GetInt32();
    }

    private async Task<int?> SearchPartnerByPhoneAsync(string db, int uid, string apiKey, string phone)
    {
        // Ham "phone", ham "mobile" maydonidan qidiramiz — Odoo'da qaysi biriga
        // yozilgani bizga noma'lum.
        var domain = new object[]
        {
            new object[] { "|", new object[] { "phone", "=", phone }, new object[] { "mobile", "=", phone } }
        };
        var kwargs = new Dictionary<string, object> { ["fields"] = new[] { "id" }, ["limit"] = 1 };

        var result = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner", "search_read", domain, kwargs });

        var items = result.EnumerateArray();
        foreach (var item in items)
        {
            return item.GetProperty("id").GetInt32();
        }

        return null;
    }

    private async Task<int> CreatePartnerAsync(string db, int uid, string apiKey, string fullName, string phone, string email)
    {
        var values = new Dictionary<string, object?>
        {
            ["name"] = string.IsNullOrWhiteSpace(fullName) ? phone : fullName,
            ["phone"] = phone,
            ["customer_rank"] = 1
        };
        if (!string.IsNullOrWhiteSpace(email)) values["email"] = email;

        var result = await CallAsync("object", "execute_kw",
            new object[] { db, uid, apiKey, "res.partner", "create", new object[] { values } });

        return result.GetInt32();
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
