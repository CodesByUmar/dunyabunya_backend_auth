namespace AuthApi.Models;

/// <summary>
/// Odoo'dan sinxronlangan mahsulot — Odoo'da product.product (variant) darajasida,
/// faqat is_published=true bo'lganlar saqlanadi (OdooProductService.GetPublishedProductsAsync
/// orqali). Bitta "asosiy mahsulot" (product.template) bir nechta variantga ega
/// bo'lishi mumkin (masalan turli amper/o'lcham) — har biri alohida qator sifatida saqlanadi.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public int OdooProductId { get; set; }
    public int OdooTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DefaultCode { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public string? CategoryName { get; set; }
    public string? Brand { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
