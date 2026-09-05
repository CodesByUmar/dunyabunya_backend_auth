using AuthApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class ProductCategoryService : IProductCategoryService
{
    private readonly AppDbContext _db;

    public ProductCategoryService(AppDbContext db)
    {
        _db = db;
    }

    // Kalit — admin ko'radigan va tanlaydigan nom (Categories jadvalidagi NameUz
    // bilan BIR XIL, mijoz saytda ko'radigan nom). Qiymat — Odoo'ning o'zidagi
    // (ichki) original nom, faqat CategoryName yo'lini qayta tuzishda ishlatiladi
    // va frontenddagi CATEGORY_SLUG_MAP (src/lib/api.ts) kalitlariga mos kelishi
    // SHART — aks holda mahsulot katalog filtrida "kategoriyasiz" bo'lib qoladi.
    // Bu yerda o'zgartirilsa, frontenddagi map bilan albatta sinxronlab turilishi kerak.
    private static readonly Dictionary<string, string> CategoryDisplayToOdoo = new()
    {
        ["Elektr"] = "Elektrika",
        ["Santexnika"] = "Muhandislik tizimlari",
        ["Qurilish materiallari"] = "Qurilish mahsulotlari",
        ["Bezak materiallari"] = "Yakuniy qoplamalar",
        ["Mahkamlash"] = "Mahkamlagichlar",
        ["Asboblar"] = "Instrumentlar"
    };

    // Odoo'ning ichki nomidan (CategoryName yo'lining birinchi bo'lagi) to'g'ridan-to'g'ri
    // /api/categories jadvalidagi Slug'ga o'tadigan xarita — frontend BUNI ishlatishi kerak,
    // matn/nom moslashtirish (fragile) o'rniga. Har bir mahsulot javobida tayyor
    // "categorySlug" maydoni sifatida beriladi (GetCategorySlug orqali).
    private static readonly Dictionary<string, string> OdooToCategorySlug = new()
    {
        ["Elektrika"] = "elektrika",
        ["Muhandislik tizimlari"] = "santekhnika",
        ["Qurilish mahsulotlari"] = "stroitelnye-materialy",
        ["Yakuniy qoplamalar"] = "otdelochnye-materialy",
        ["Mahkamlagichlar"] = "krypyozh",
        ["Instrumentlar"] = "instrumenty"
    };

    // Mahsulotning CategoryName'idan ("Hammasi / Muhandislik tizimlari / Adapter")
    // to'g'ridan-to'g'ri, ISHONCHLI slug ("santekhnika") ni hisoblaydi — bu slug
    // GET /api/categories'dagi Slug bilan AYNAN bir xil, frontend hech qanday nom
    // moslashtirish/taxmin qilmasdan to'g'ridan-to'g'ri solishtira oladi.
    public string? GetCategorySlug(string? categoryName)
    {
        var (top, _) = ParseCategoryPath(categoryName);
        return top != null && OdooToCategorySlug.TryGetValue(top, out var slug) ? slug : null;
    }

    // "Hammasi / Elektrika / Past kuchlanishli uskunalar / AVR" -> ("Elektrika", "AVR").
    // Frontend'ning src/lib/api.ts:mapBackendCategoryPath bilan bir xil mantiq —
    // birinchi (Hammasi'dan keyingi) bo'lak "kategoriya", oxirgi bo'lak "subkategoriya".
    public (string? Top, string? Leaf) ParseCategoryPath(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return (null, null);

        var segments = categoryName.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s != "Hammasi")
            .ToList();

        if (segments.Count == 0) return (null, null);
        return (segments[0], segments[^1]);
    }

    // Odoo'ning ichki nomini ("Muhandislik tizimlari") mijoz/admin ko'radigan nomga
    // ("Santexnika") o'giradi — admin panelning category-options ro'yxatidagi bilan
    // bir xilini ko'rsatish uchun (masalan admin tahrirlash formasida).
    public string? ToDisplayCategory(string? odooCategoryTop) =>
        odooCategoryTop != null ? CategoryDisplayToOdoo.FirstOrDefault(kv => kv.Value == odooCategoryTop).Key : null;

    // Admin panelda "Kategoriya" va "Subkategoriya"ni SELECT orqali tanlash uchun —
    // ikkalasi ham erkin matn EMAS. Kategoriya sifatida MIJOZ ko'radigan nom
    // qaytariladi (masalan "Santexnika"), Odoo'ning ichki nomi ("Muhandislik
    // tizimlari") admin'ga umuman ko'rsatilmaydi — shunday bo'lmasa admin
    // saytdagi qaysi bo'limga tegishli ekanini bilolmay chalkashadi.
    //
    // MUHIM (2026-08-30 tuzatildi): Subkategoriya endi Odoo'dan xom kelgan
    // qiymatlar EMAS — Categories/Subcategories jadvalidagi (mijoz katalogda
    // ko'radigan, kurator qilingan) nomlar. Sabab: Odoo'ning xom subkategoriya
    // nomlari (masalan "AVR") mijoz katalogining subkategoriya filtridagi
    // slug'lar bilan HECH QACHON mos kelmasdi (butunlay boshqa lug'at), shuning
    // uchun subkategoriya bo'yicha filtrlash har doim bo'sh natija berardi.
    // Endi admin xuddi mijoz ko'radigan bo'limlardan birini tanlaydi — shuning
    // uchun ResolveCategoryChangeAsync endi kafolatlangan to'g'ri Subcategory.Slug'ni
    // saqlay oladi.
    public async Task<List<CategoryOptionView>> GetCategoryOptionsAsync()
    {
        var categories = await _db.Categories
            .Include(c => c.Subcategories)
            .Where(c => OdooToCategorySlug.Values.Contains(c.Slug))
            .ToListAsync();
        var categoriesBySlug = categories.ToDictionary(c => c.Slug);

        // O'zbekcha nomlar Translations jadvalidan ("data.subcategories.{slug}") —
        // CategoriesController'dagi bilan bir xil manba, ikkalasi sinxron turadi.
        var allSlugs = categories.SelectMany(c => c.Subcategories).Select(s => s.Slug).ToList();
        var uzByKey = await GetUzSubcategoryTranslationsAsync(allSlugs);

        return CategoryDisplayToOdoo.Select(kv =>
        {
            var categorySlug = OdooToCategorySlug[kv.Value];
            var subcategories = categoriesBySlug.TryGetValue(categorySlug, out var cat)
                ? cat.Subcategories.OrderBy(s => s.Order).Select(s => new SubcategoryOptionView(
                    s.Slug,
                    s.NameRu,
                    uzByKey.GetValueOrDefault($"data.subcategories.{s.Slug}", s.NameRu)
                )).ToList()
                : new List<SubcategoryOptionView>();

            return new CategoryOptionView(kv.Key, subcategories);
        }).ToList();
    }

    // MUHIM (tuzatildi): avval bu FAQAT newCategoryDisplay berilganda ishga
    // tushardi. Agar admin faqat Subkategoriyani o'zgartirsa (Kategoriya
    // allaqachon to'g'ri bo'lgani uchun frontend uni "o'zgarmagan" deb
    // umuman yubormasa) — Subkategoriya ham butunlay e'tiborsiz qoldirilar
    // edi (mahsulot "Ichki kategoriyasiz"da qolib ketardi). Endi
    // newCategoryDisplay null kelsa, currentCategoryName asos qilib olinadi.
    public async Task<CategoryResolutionResult> ResolveCategoryChangeAsync(string? currentCategoryName, string? newCategoryDisplay, string? newSubcategory)
    {
        string odooCategory;

        if (newCategoryDisplay != null)
        {
            // newCategoryDisplay — mijoz ko'radigan nom (masalan "Santexnika"). Odoo'ning
            // ichki nomiga ("Muhandislik tizimlari") shu orqali o'giriladi.
            if (!CategoryDisplayToOdoo.TryGetValue(newCategoryDisplay, out var mappedCategory))
            {
                return CategoryResolutionResult.Fail($"Kategoriya faqat shulardan biri bo'lishi kerak: {string.Join(", ", CategoryDisplayToOdoo.Keys)}");
            }
            odooCategory = mappedCategory;
        }
        else
        {
            var (currentTop, _) = ParseCategoryPath(currentCategoryName);
            if (currentTop == null || !OdooToCategorySlug.ContainsKey(currentTop))
            {
                return CategoryResolutionResult.Fail("Subkategoriyani belgilashdan oldin avval Kategoriyani tanlang.");
            }
            odooCategory = currentTop;
        }

        if (string.IsNullOrWhiteSpace(newSubcategory))
        {
            return CategoryResolutionResult.Fail("Subkategoriya bo'sh bo'lishi mumkin emas.");
        }

        // Subkategoriya ham erkin matn EMAS — faqat shu Kategoriya ostida
        // Categories/Subcategories jadvalidagi (mijoz katalogda ko'radigan,
        // kurator qilingan) nomlardan biri bo'lishi kerak (GetCategoryOptionsAsync
        // shu ro'yxatni beradi). Frontend ham slug ("avr"), ham ko'rsatiladigan
        // nom ("AVR") yuborishi mumkin — ikkalasi ham qabul qilinadi, katta-kichik
        // harfga sezgir emas.
        var categorySlug = OdooToCategorySlug[odooCategory];
        var candidate = newSubcategory.Trim().ToLower();
        var subcategoryRow = await _db.Subcategories
            .Include(s => s.Category)
            .Where(s => s.Category.Slug == categorySlug)
            .FirstOrDefaultAsync(s => s.NameRu.ToLower() == candidate || s.Slug.ToLower() == candidate);

        if (subcategoryRow == null)
        {
            var known = await _db.Subcategories
                .Where(s => s.Category.Slug == categorySlug)
                .Select(s => s.NameRu)
                .ToListAsync();

            var displayCategory = newCategoryDisplay ?? ToDisplayCategory(odooCategory);
            return CategoryResolutionResult.Fail(known.Count > 0
                ? $"\"{displayCategory}\" kategoriyasi uchun subkategoriya faqat shulardan biri bo'lishi kerak: {string.Join(", ", known)}"
                : $"\"{displayCategory}\" kategoriyasi uchun hali hech qanday ma'lum subkategoriya yo'q.");
        }

        // "Hammasi / {OdooCategory} / {Subcategory}" — frontend faqat birinchi va
        // oxirgi bo'lakni o'qiydi, o'rtadagi bo'lak muhim emas (q. ParseCategoryPath).
        // Leaf matn har doim kanonik (kurator) nom — newSubcategory slug shaklida
        // yuborilgan bo'lsa ham, ko'rsatiladigan joylarda doim o'qiladigan nom
        // chiqishi uchun.
        var categoryName = $"Hammasi / {odooCategory} / {subcategoryRow.NameRu}";
        return CategoryResolutionResult.Ok(categoryName, subcategoryRow.Slug);
    }

    private async Task<Dictionary<string, string>> GetUzSubcategoryTranslationsAsync(List<string> slugs)
    {
        if (slugs.Count == 0) return new Dictionary<string, string>();

        var keys = slugs.Select(s => $"data.subcategories.{s}").ToList();
        return await _db.Translations
            .Where(t => t.App == "user" && keys.Contains(t.Key))
            .ToDictionaryAsync(t => t.Key, t => t.Uz);
    }
}
