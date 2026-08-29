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

    // Admin panel orqali Name qo'lda tahrirlangan bo'lsa true bo'ladi —
    // ProductSyncBackgroundService shundan keyin bu maydonga endi tegmaydi (Odoo'dan
    // kelgan qiymat bilan ustidan yozib yubormaydi). Narx (Price) va Kategoriya
    // (CategoryName) admin tomonidan tahrirlanmaydi — doim Odoo'dan sinxronlanadi
    // (CategoryName frontendning kategoriya-filtrlash mantig'i original Odoo yo'liga
    // qattiq bog'langan — uni o'zgartirish katalogda "kategoriyasiz" bo'lib qolishga
    // olib keladi, frontend to'g'ri tuzatilmaguncha bu maydon tahrirlanmaydi).
    public bool NameOverridden { get; set; }

    // Odoo'dan YANGI kelgan mahsulot avtomatik "pending" bilan saqlanadi va admin
    // tasdiqlamaguncha ochiq katalogda (GET /api/Products) ko'rinmaydi. Sync xizmati
    // faqat yangi qatorlar uchun "pending" qo'yadi — mavjud mahsulotni yangilashda bu
    // maydonga tegilmaydi, shuning uchun admin qarori keyingi sinxronizatsiyalarda
    // yo'qolmaydi. "approved" | "pending" | "rejected".
    public string ApprovalStatus { get; set; } = "approved";

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

    // Odoo'da bu mahsulotlar uchun tavsif/xususiyat kiritilmagan (tekshirilgan),
    // shuning uchun admin panel orqali qo'lda to'ldiriladi — sync bu maydonga
    // tegmaydi (rasm kabi xavfsiz).
    public string? Description { get; set; }

    public List<ProductImage> Images { get; set; } = new();
    public List<ProductSpecification> Specifications { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Asosiy rasmdan tashqari qo'shimcha galereya rasmlari — faqat admin panel
// (yoki "products" ruxsatiga ega Superuser) orqali qo'lda qo'shiladi/o'chiriladi.
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ImageBase64 { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Xususiyatlar jadvali (masalan "Akkumulyator" -> "18 V Li-Ion") — Odoo'da
// mavjud emas, faqat admin panel orqali qo'lda kiritiladi.
public class ProductSpecification
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Order { get; set; }
}
