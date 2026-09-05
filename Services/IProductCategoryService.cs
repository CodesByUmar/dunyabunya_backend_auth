namespace AuthApi.Services;

public record SubcategoryOptionView(string Slug, string NameRu, string NameUz);
public record CategoryOptionView(string Category, List<SubcategoryOptionView> Subcategories);

// Subkategoriya tanlashda muvaffaqiyatli natija (CategoryName+SubcategorySlug) yoki
// admin'ga ko'rsatiladigan tayyor xato xabari — ProductsController.UpdateProductDetails
// shunchaki BadRequest(ErrorMessage) yoki mahsulotga CategoryName/SubcategorySlug'ni
// yozadi, validatsiya mantig'ining o'ziga aralashmaydi.
public record CategoryResolutionResult(bool Success, string? CategoryName, string? SubcategorySlug, string? ErrorMessage)
{
    public static CategoryResolutionResult Fail(string message) => new(false, null, null, message);
    public static CategoryResolutionResult Ok(string categoryName, string subcategorySlug) => new(true, categoryName, subcategorySlug, null);
}

// ProductsController'dan chiqarilgan — Odoo'ning ichki kategoriya nomlari bilan
// mijoz/admin ko'radigan nomlar/slug'lar orasidagi barcha moslashtirish mantig'i
// (q. ProductCategoryService'dagi tafsilotlar).
public interface IProductCategoryService
{
    string? GetCategorySlug(string? categoryName);
    (string? Top, string? Leaf) ParseCategoryPath(string? categoryName);
    string? ToDisplayCategory(string? odooCategoryTop);
    Task<List<CategoryOptionView>> GetCategoryOptionsAsync();

    // dto.Category/dto.Subcategory'ni Odoo formatidagi CategoryName'ga va kafolatlangan
    // SubcategorySlug'ga aylantiradi, yo'lda validatsiya qiladi. currentCategoryName —
    // newCategoryDisplay null bo'lganda (faqat Subkategoriya yuborilganda) mahsulotning
    // HOZIRGI kategoriyasini asos qilib olish uchun.
    Task<CategoryResolutionResult> ResolveCategoryChangeAsync(string? currentCategoryName, string? newCategoryDisplay, string? newSubcategory);
}
