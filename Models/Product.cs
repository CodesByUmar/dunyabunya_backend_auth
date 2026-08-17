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
    public bool InStock { get; set; }

    // Sharxlardan avtomatik hisoblanadi (ReviewsController) — frontend to'g'ridan-to'g'ri
    // o'zgartira olmaydi, faqat sharh qo'shilganda/o'chirilganda server yangilaydi.
    public double Rating { get; set; }
    public int ReviewCount { get; set; }

    // Odoo'dan olingan rasm (image_128 — katalog kartochkasi uchun kichik
    // o'lcham, katta rasm butun ro'yxatni sekinlashtirib, so'rovni timeout
    // qilib qo'ygan edi), base64 — mavjud bo'lsa. Ro'yxat
    // endpointida o'zi emas, faqat shunga asoslangan URL qaytariladi
    // (katta JSON payload'dan qochish uchun), rasm alohida endpoint orqali beriladi.
    public string? ImageBase64 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
