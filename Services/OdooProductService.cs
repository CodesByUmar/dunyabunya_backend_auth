using System.Net.Http.Json;
using System.Text.Json;

namespace AuthApi.Services;

/// <summary>
/// Odoo'dan is_published=true bo'lgan mahsulot VARIANTLARINI (product.product) JSON-RPC
/// orqali tortib oladi. Ikkita muhim narsa audit orqali aniqlangan:
///
/// 1) is_published product.template darajasida emas, product.product (variant)
///    darajasida tekshirilishi kerak — bitta asosiy mahsulotning bir nechta varianti
///    (masalan turli amper/o'lcham) bo'lishi mumkin, har biri alohida SKU.
///
/// 2) Narx list_price'dan emas, "Websayt" nomli pricelist (id=4)dan olinadi
///    (product.pricelist.item, fixed_price) — bu haqiqiy sotuv narxi.
///
/// Brend — "Brend" (attribute_id=11199) nomli variant xususiyati sifatida saqlangan,
/// template darajasida (product.template.attribute.line -> value_ids ->
/// product.attribute.value.name).
/// </summary>
public class OdooProductService : IOdooProductService
{
    private const int BrendAttributeId = 11199;
    private const int WebsitePricelistId = 4;

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

        // 1) is_published=true bo'lgan barcha VARIANTLAR (product.product).
        // MUHIM: "limit" va "order" ANIQ ko'rsatilishi SHART. Aks holda Odoo'ning
        // o'zi ba'zan (aniqlanmagan ichki sabablarga ko'ra — ehtimol standart
        // tartiblanmagan natija to'plamining "chekka"sida) bir xil so'rovga har safar
        // BIRXIL SONDAGI, lekin BOSHQA-BOSHQA ID'lardan iborat natija qaytarishi
        // kuzatildi — natijada allaqachon tasdiqlangan mahsulot navbatdagi
        // sinxronizatsiyada Odoo ro'yxatida "yo'q" bo'lib chiqib, qayta "pending"ga
        // o'tkazib yuborilardi. Aniq order + katta limit shu nomuvofiqlikni bartaraf qiladi.
        var variants = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "product.product", "search_read",
            new object[] { new object[] { new object[] { "is_published", "=", true } } },
            new Dictionary<string, object>
            {
                ["fields"] = new[] { "id", "display_name", "default_code", "barcode", "standard_price", "categ_id", "product_tmpl_id", "qty_available" },
                ["order"] = "id asc",
                ["limit"] = 100000
            }
        });
        var variantList = variants.EnumerateArray().ToList();
        if (variantList.Count == 0) return new List<OdooProductDto>();

        var variantIds = variantList.Select(v => v.GetProperty("id").GetInt32()).ToArray();
        var templateIds = variantList.Select(v => v.GetProperty("product_tmpl_id")[0].GetInt32()).Distinct().ToArray();

        // 2) "Websayt" pricelist'dan har bir variant uchun narx
        var priceByVariantId = await GetWebsitePricesAsync(db, uid, apiKey, variantIds);

        // 3) Har bir template uchun brend (attribute_id=11199 "Brend")
        var brandByTemplateId = await GetBrandsByTemplateAsync(db, uid, apiKey, templateIds);

        // 4) Yakuniy ro'yxatni yig'amiz
        var result = new List<OdooProductDto>();
        foreach (var v in variantList)
        {
            var id = v.GetProperty("id").GetInt32();
            var templateId = v.GetProperty("product_tmpl_id")[0].GetInt32();

            string? categoryName = null;
            if (v.TryGetProperty("categ_id", out var categ) && categ.ValueKind == JsonValueKind.Array)
            {
                var arr = categ.EnumerateArray().ToList();
                if (arr.Count > 1) categoryName = arr[1].GetString();
            }

            priceByVariantId.TryGetValue(id, out var price);
            brandByTemplateId.TryGetValue(templateId, out var brand);

            var qtyAvailable = v.TryGetProperty("qty_available", out var qty) && qty.ValueKind == JsonValueKind.Number
                ? qty.GetDouble()
                : 0;

            result.Add(new OdooProductDto(
                OdooProductId: id,
                OdooTemplateId: templateId,
                Name: CleanDisplayName(v.GetProperty("display_name").GetString()),
                DefaultCode: GetStringOrNull(v, "default_code"),
                Barcode: GetStringOrNull(v, "barcode"),
                Price: price,
                Cost: (decimal)v.GetProperty("standard_price").GetDouble(),
                CategoryName: categoryName,
                Brand: brand,
                InStock: qtyAvailable > 0
            ));
        }

        return result;
    }

    private async Task<Dictionary<int, decimal>> GetWebsitePricesAsync(string db, int uid, string apiKey, int[] variantIds)
    {
        var prices = new Dictionary<int, decimal>();

        var items = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "product.pricelist.item", "search_read",
            new object[]
            {
                new object[]
                {
                    new object[] { "pricelist_id", "=", WebsitePricelistId },
                    new object[] { "product_id", "in", variantIds.Cast<object>().ToArray() }
                }
            },
            new Dictionary<string, object> { ["fields"] = new[] { "product_id", "fixed_price", "compute_price" }, ["limit"] = 100000 }
        });

        foreach (var item in items.EnumerateArray())
        {
            // Hozircha faqat "fixed" turini qo'llab-quvvatlaymiz — audit paytida
            // barcha 296 ta published mahsulot shu turda ekani tasdiqlangan edi.
            var computeType = item.GetProperty("compute_price").GetString();
            if (computeType != "fixed") continue;

            var variantId = item.GetProperty("product_id")[0].GetInt32();
            prices[variantId] = (decimal)item.GetProperty("fixed_price").GetDouble();
        }

        return prices;
    }

    private async Task<Dictionary<int, string>> GetBrandsByTemplateAsync(string db, int uid, string apiKey, int[] templateIds)
    {
        var result = new Dictionary<int, string>();

        var lines = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "product.template.attribute.line", "search_read",
            new object[] { new object[] { new object[] { "product_tmpl_id", "in", templateIds.Cast<object>().ToArray() }, new object[] { "attribute_id", "=", BrendAttributeId } } },
            new Dictionary<string, object> { ["fields"] = new[] { "product_tmpl_id", "value_ids" }, ["limit"] = 100000 }
        });

        var templateToBrandValueId = new Dictionary<int, int>();
        var brandValueIds = new HashSet<int>();
        foreach (var line in lines.EnumerateArray())
        {
            var tmplId = line.GetProperty("product_tmpl_id")[0].GetInt32();
            var valueIds = line.GetProperty("value_ids").EnumerateArray().Select(x => x.GetInt32()).ToList();
            if (valueIds.Count == 0) continue;

            templateToBrandValueId[tmplId] = valueIds[0];
            brandValueIds.Add(valueIds[0]);
        }

        if (brandValueIds.Count == 0) return result;

        var values = await CallAsync("object", "execute_kw", new object[]
        {
            db, uid, apiKey, "product.attribute.value", "read",
            new object[] { brandValueIds.Cast<object>().ToArray() },
            new Dictionary<string, object> { ["fields"] = new[] { "id", "name" } }
        });
        var brandNames = new Dictionary<int, string>();
        foreach (var v in values.EnumerateArray())
        {
            brandNames[v.GetProperty("id").GetInt32()] = v.GetProperty("name").GetString() ?? "";
        }

        foreach (var (tmplId, valueId) in templateToBrandValueId)
        {
            if (brandNames.TryGetValue(valueId, out var name)) result[tmplId] = name;
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

    // Odoo display_name formati: "[default_code] Nom (variant xususiyati)" — masalan
    // "[0001-00001] AVR CHINT (NXZM-125S/3P-80A)". default_code'ni alohida maydonda
    // saqlaymiz, shuning uchun bu yerda boshidagi "[...] " qismini olib tashlaymiz,
    // qavs ichidagi variant farqi ("(NXZM-...)") esa saqlanib qoladi.
    private static string CleanDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(displayName, @"^\[[^\]]*\]\s*(.*)$");
        return match.Success ? match.Groups[1].Value : displayName;
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
