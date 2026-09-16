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
    // Odoo'dan keladigan asl nom (odatda rus tilida) — mijoz katalogida va
    // admin ro'yxatlarida shu ishlatiladi. O'zbekcha nom uchun NameUz (pastda).
    public string Name { get; set; } = string.Empty;

    // Nomning o'zbekcha varianti — Odoo'da bunday maydon yo'q, faqat admin panel
    // orqali qo'lda kiritiladi (DescriptionRu/DescriptionUz bilan bir xil uslub —
    // sync bu maydonga hech qachon tegmaydi, "overridden" bayrog'i kerak emas,
    // chunki ustidan yozib yuborish uchun Odoo'dan kelgan qiymat umuman yo'q).
    public string? NameUz { get; set; }

    public string? DefaultCode { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public string? CategoryName { get; set; }
    public string? Brand { get; set; }
    public bool InStock { get; set; }

    // Odoo ombordagi HAQIQIY son (qty_available) — faqat ADMIN paneliga
    // ko'rsatish uchun (GetProductForAdmin/admin-list/pending). Ochiq (mijozga
    // ko'rinadigan) endpointlarga chiqarilMAYDI — u yerda faqat InStock
    // (bor/yo'q) ishlatilaveradi, xuddi avvalgidek. 2026-09-16.
    public int StockQuantity { get; set; }

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

    // Odoo'dan kelgan ASL nom — MAHSULOT BIRINCHI BOR sync'da qo'shilgandagi
    // qiymat, keyin HECH QACHON o'zgarmaydi (2026-09-15 qarori: "original" =
    // tarixiy birinchi nom; Odoo'da display_name o'zgarsa ham bu maydon
    // siljiymaydi — ProductSyncBackgroundService uni faqat ADD qilishda yozadi,
    // mavjud qatorlarga sync HECH QACHON tegmaydi). Odoo'dagi hozirgi nom esa
    // Name maydonida yashaydi (admin tahrir qilmaguncha sync yangilab turadi).
    public string? OdooOriginalName { get; set; }

    // Odoo'dagi ASL (admin tahriridan mustaqil) kategoriya nomi — OdooOriginalName'dan
    // farqli o'laroq, sync har safar Odoo'dan kelgan qiymat bilan yangilab turadi
    // (CategoryNameOverridden'ga qaramasdan). Admin CategoryName'ni tahrirlasa,
    // asl Odoo qiymati shu yerda "orqa fonda" saqlanib qoladi — yo'qolib ketmaydi.
    public string? OdooOriginalCategoryName { get; set; }

    // Admin PATCH /details orqali tanlagan subkategoriyaning Subcategories
    // jadvalidagi (mijoz katalogda ko'radigan, kurator qilingan) Slug'i —
    // CategoryName'dagi Odoo'ning xom oxirgi bo'lagidan farqli o'laroq, bu
    // maydon GET /api/categories'dagi Subcategory.Slug bilan AYNAN bir xil,
    // shuning uchun frontend katalogning subkategoriya filtri to'g'ri ishlashi
    // uchun to'g'ridan-to'g'ri solishtira oladi. Tanlanmagan bo'lsa null.
    public string? SubcategorySlug { get; set; }

    // ESKI, ENDI ISHLATILMAYDI (2026-09-12'da IsOnline'ga almashtirildi) — bazada
    // faqat tarixiy ma'lumot sifatida qoladi, kod ichida hech qayerda o'qilmaydi/
    // yozilmaydi. O'chirib tashlash xavfsiz emas deb topilib, atayin saqlab qo'yilgan.
    public string ApprovalStatus { get; set; } = "approved";

    // Admin panelda "Online/Offline" tugmasi/dropdown'i shu yerni boshqaradi —
    // istalgan vaqt ikki tomonga (Online<->Offline) almashtiriladi (eski uch holatli
    // ApprovalStatus'ning o'rnini bosadi). Odoo'dan YANGI kelgan mahsulot avtomatik
    // "false" (Offline) bilan saqlanadi, admin ko'rib chiqib "Online" qilmaguncha
    // ochiq katalogda ko'rinmaydi. Sync xizmati faqat yangi qatorlar uchun "false"
    // qo'yadi — mavjud mahsulotni yangilashda bu maydonga tegilmaydi, shuning uchun
    // admin qarori keyingi sinxronizatsiyalarda yo'qolmaydi.
    //
    // IsPublishedInOdoo'dan MUSTAQIL: ochiq katalog (GET /api/products) ikkalasi
    // ham true bo'lgandagina mahsulotni ko'rsatadi.
    public bool IsOnline { get; set; }

    // Mahsulot HOZIR Odoo'da is_published=true ro'yxatida bormi — ApprovalStatus'dan
    // MUSTAQIL. Admin tasdig'i (ApprovalStatus) doim saqlanib qoladi, bu maydon esa
    // faqat KO'RINISHNI boshqaradi: ochiq katalog (GET /api/products) endi ikkalasi
    // ham true bo'lgandagina mahsulotni ko'rsatadi. Admin Odoo'da is_published'ni
    // o'chirsa — mahsulot saytdan darhol yashiriladi (qayta tasdiqlash TALAB
    // QILINMAYDI); qaytadan yoqsa — o'zi qaytadan ko'rinadi. Yangi mahsulot
    // qo'shilganda doim true (chunki u aynan shu sabab — is_published=true — bilan
    // topilgan).
    public bool IsPublishedInOdoo { get; set; } = true;

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
