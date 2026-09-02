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

    // Admin panel orqali Name/CategoryName qo'lda tahrirlangan bo'lsa true bo'ladi —
    // ProductSyncBackgroundService shundan keyin bu maydonlarga endi tegmaydi (Odoo'dan
    // kelgan qiymat bilan ustidan yozib yubormaydi). Narx (Price) admin tomonidan
    // umuman tahrirlanmaydi — doim Odoo'dan sinxronlanadi.
    //
    // MUHIM: CategoryName frontendning kategoriya-filtrlash mantig'i faqat ma'lum
    // (qattiq yozilgan) original Odoo yo'liga mos kelganda ishlaydi. Shuning uchun
    // bu maydon ERKIN matn sifatida TAHRIRLANMAYDI — faqat ProductsController'dagi
    // CategoryOptions ro'yxatidan tanlangan (kafolatlangan to'g'ri) qiymat orqali
    // "Hammasi / {category} / {subcategory}" shaklida qayta tuzilib yoziladi.
    public bool NameOverridden { get; set; }
    public bool CategoryNameOverridden { get; set; }

    // Odoo'dagi ASL (admin tahriridan mustaqil) Nom/Kategoriya — sync har safar
    // bularni Odoo'dan kelgan qiymat bilan yangilab turadi, NameOverridden/
    // CategoryNameOverridden'ga qaramasdan. Admin Name/CategoryName'ni tahrirlasa,
    // asl Odoo qiymati shu yerda "orqa fonda" saqlanib qoladi — yo'qolib ketmaydi.
    public string? OdooOriginalName { get; set; }
    public string? OdooOriginalCategoryName { get; set; }

    // Admin PATCH /details orqali tanlagan subkategoriyaning Subcategories
    // jadvalidagi (mijoz katalogda ko'radigan, kurator qilingan) Slug'i —
    // CategoryName'dagi Odoo'ning xom oxirgi bo'lagidan farqli o'laroq, bu
    // maydon GET /api/categories'dagi Subcategory.Slug bilan AYNAN bir xil,
    // shuning uchun frontend katalogning subkategoriya filtri to'g'ri ishlashi
    // uchun to'g'ridan-to'g'ri solishtira oladi. Tanlanmagan bo'lsa null.
    public string? SubcategorySlug { get; set; }

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
    // tegmaydi (rasm kabi xavfsiz). Ikkala til alohida saqlanadi (Banners'dagi
    // TitleRu/TitleUz bilan bir xil uslub) — Translations jadvali orqali emas,
    // chunki bu matn faqat shu bitta mahsulotga tegishli, boshqa joyda
    // qayta ishlatilmaydi.
    public string? DescriptionRu { get; set; }
    public string? DescriptionUz { get; set; }

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
// mavjud emas, faqat admin panel orqali qo'lda kiritiladi. Kalit va qiymat
// ikkalasi ham ikki tilda alohida saqlanadi.
public class ProductSpecification
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string KeyRu { get; set; } = string.Empty;
    public string KeyUz { get; set; } = string.Empty;
    public string ValueRu { get; set; } = string.Empty;
    public string ValueUz { get; set; } = string.Empty;
    public int Order { get; set; }
}
