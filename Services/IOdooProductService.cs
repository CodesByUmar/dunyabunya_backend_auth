namespace AuthApi.Services;

public record OdooProductDto(
    int OdooProductId,
    int OdooTemplateId,
    string Name,
    string? DefaultCode,
    string? Barcode,
    decimal Price,
    decimal Cost,
    string? CategoryName,
    string? Brand,
    bool InStock,
    // Odoo'dagi HAQIQIY ombordagi son (qty_available) — faqat ADMIN uchun
    // (ProductsController'da mijozlarga ochiq endpointlarga chiqarilmaydi,
    // faqat InStock bool sifatida ko'rinadi). 2026-09-16.
    int StockQuantity
);

/// <summary>
/// Bitta mahsulotning Odoo'dagi HOZIRGI (jonli) holati — admin paneldagi
/// "Odoo'da tekshirish" uchun (GET /api/products/{id}/odoo-info). Bazadagi
/// nusxa (Product) va tarixiy nom (OdooOriginalName) bilan solishtirish uchun.
/// </summary>
public record OdooCurrentProductInfo(
    int OdooProductId,
    int OdooTemplateId,
    string Name,
    string? DefaultCode,
    string? Barcode,
    decimal Price,
    decimal Cost,
    string? CategoryName,
    string? Brand,
    bool InStock,
    int StockQuantity,
    bool IsPublishedInOdoo
);

public interface IOdooProductService
{
    /// <summary>
    /// Odoo'da is_published=true bo'lgan barcha mahsulot VARIANTLARINI (product.product)
    /// qaytaradi — brend va "Websayt" pricelist narxi bilan.
    /// </summary>
    Task<List<OdooProductDto>> GetPublishedProductsAsync();

    /// <summary>
    /// BITTA mahsulotni OdooProductId bo'yicha jonli so'raydi — is_published FILTRISIZ
    /// (ya'ni Odoo'da yashirib qo'yilgan mahsulot ham topiladi; sync'dagi "faqat
    /// nashr etilganlar" cheklovi bu yerda ATAYIN yo'q). Admin bitta mahsulotni
    /// ochgandagina chaqiriladi — ro'yxatlar uchun MO' LJAL EMAS (N+1). Odoo'da
    /// topilmasa (o'chirilgan bo'lsa) null qaytaradi.
    /// </summary>
    Task<OdooCurrentProductInfo?> GetProductInfoByIdAsync(int odooProductId);
}
