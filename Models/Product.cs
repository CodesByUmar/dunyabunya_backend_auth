namespace AuthApi.Models;

/// <summary>
/// Odoo'dan sinxronlangan mahsulot — faqat is_published=true bo'lganlar saqlanadi
/// (OdooProductService.GetPublishedProductsAsync orqali).
/// </summary>
public class Product
{
    public int Id { get; set; }
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
