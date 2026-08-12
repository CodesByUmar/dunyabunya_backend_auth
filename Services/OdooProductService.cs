using System.Net.Http.Json;
using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Odoo'dan is_published=true bo'lgan mahsulotlarni JSON-RPC orqali tortib oladi.
/// Brend maydon sifatida mavjud emas — u "Brend" (attribute_id=11199) nomli variant
/// xususiyati sifatida saqlangan (product.template.attribute.line -> value_ids ->
/// product.attribute.value.name). Bu ID audit orqali aniqlangan; Odoo'da "Brand"
/// nomli deyarli ishlatilmagan (24 ta) dublikat ham bor — shu sabab ishlatilmaydi.
/// </summary>
public class OdooProductService : IOdooProductService
{
    private const int BrendAttributeId = 11199;

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OdooProductService> _logger;

    public OdooProductService(HttpClient http, IConfiguration config, ILogger<OdooProductService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<List<OdooProductDto>> GetPublishedProductsAsync()
    {
        var db = _config["Odoo:Database"];
        var username = _config["Odoo:Username"];
        var apiKey = _config["Odoo:ApiKeyOutbound"];

        if (string.IsNullOrEmpty(db) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Odoo sozlamalari to'liq emas — mahsulotlarni tortib bo'lmadi.");
            return new List<OdooProductDto>();
        }

        var uid = await AuthenticateAsync(db, username, apiKey);

        // 1) is_published=true bo'lgan barcha template'lar
        var templates = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "product.template", "search_read",
            new object[] { new object[] { new object[] { "is_published", "=", true } } },
            new Dictionary<string, object>
            {
                ["fields"] = new[] { "id", "name", "default_code", "barcode", "list_price", "standard_price", "categ_id", "attribute_line_ids" }
            }
        });
        var templateList = templates.EnumerateArray().ToList();
        if (templateList.Count == 0) return new List<OdooProductDto>();

        // 2) attribute_line'lardan "Brend" (11199) bo'lganlarini ajratamiz
        var allLineIds = templateList
            .SelectMany(t => t.GetProperty("attribute_line_ids").EnumerateArray().Select(x => x.GetInt32()))
            .Distinct()
            .ToArray();

        var templateToBrandValueId = new Dictionary<int, int>();
        var brandValueIds = new HashSet<int>();

        if (allLineIds.Length > 0)
        {
            var lines = await CallAsync("object", "execute_kw", new object[]
            {
                db, uid, apiKey, "product.template.attribute.line", "read",
                new object[] { allLineIds.Cast<object>().ToArray() },
                new Dictionary<string, object> { ["fields"] = new[] { "id", "attribute_id", "value_ids", "product_tmpl_id" } }
            });

            foreach (var line in lines.EnumerateArray())
            {
                var attrId = line.GetProperty("attribute_id")[0].GetInt32();
                if (attrId != BrendAttributeId) continue;

                var tmplId = line.GetProperty("product_tmpl_id")[0].GetInt32();
                var valueIds = line.GetProperty("value_ids").EnumerateArray().Select(x => x.GetInt32()).ToList();
                if (valueIds.Count == 0) continue;

                templateToBrandValueId[tmplId] = valueIds[0];
                brandValueIds.Add(valueIds[0]);
            }
        }

        // 3) Brend qiymatlarining haqiqiy nomini olamiz
        var brandNames = new Dictionary<int, string>();
        if (brandValueIds.Count > 0)
        {
            var values = await CallAsync("object", "execute_kw", new object[]
            {
                db, uid, apiKey, "product.attribute.value", "read",
                new object[] { brandValueIds.Cast<object>().ToArray() },
                new Dictionary<string, object> { ["fields"] = new[] { "id", "name" } }
            });
            foreach (var v in values.EnumerateArray())
            {
                brandNames[v.GetProperty("id").GetInt32()] = v.GetProperty("name").GetString() ?? "";
            }
        }

        // 4) Yakuniy ro'yxatni yig'amiz
        var result = new List<OdooProductDto>();
        foreach (var t in templateList)
        {
            var id = t.GetProperty("id").GetInt32();
            string? brand = null;
            if (templateToBrandValueId.TryGetValue(id, out var vid) && brandNames.TryGetValue(vid, out var bn))
            {
                brand = bn;
            }

            string? categoryName = null;
            if (t.TryGetProperty("categ_id", out var categ) && categ.ValueKind == JsonValueKind.Array)
            {
                var arr = categ.EnumerateArray().ToList();
                if (arr.Count > 1) categoryName = arr[1].GetString();
            }

            result.Add(new OdooProductDto(
                OdooTemplateId: id,
                Name: t.GetProperty("name").GetString() ?? "",
                DefaultCode: GetStringOrNull(t, "default_code"),
                Barcode: GetStringOrNull(t, "barcode"),
                Price: (decimal)t.GetProperty("list_price").GetDouble(),
                Cost: (decimal)t.GetProperty("standard_price").GetDouble(),
                CategoryName: categoryName,
                Brand: brand
            ));
        }

        return result;
    }

    private async Task<int> AuthenticateAsync(string db, string username, string apiKey)
    {
        var result = await CallAsync("common", "authenticate",
            new object[] { db, username, apiKey, new Dictionary<string, object>() });
        return result.GetInt32();
    }

    private static string? GetStringOrNull(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
