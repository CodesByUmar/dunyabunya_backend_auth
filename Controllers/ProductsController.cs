using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Frontend uchun — Odoo'dan sinxronlangan (is_published=true) mahsulotlar.
// Ochiq (public), autentifikatsiya talab qilinmaydi — katalog hammaga ko'rinadi.
//
// MUHIM: Narx/Brend/Ombor holati (Price/Brand/InStock) har doim Odoo'dan
// avtomatik qayta yoziladi (ProductSyncBackgroundService) — bularni
// o'zgartiradigan endpoint YO'Q. Nomi (Name) admin panel orqali erkin
// tahrirlanadi. Kategoriya (CategoryName) esa ERKIN MATN sifatida
// tahrirlanmaydi — frontendning kategoriya-filtrlash mantig'i faqat
// CategoryOptions ro'yxatidagi (pastda) original Odoo nomlariga mos kelganda
// ishlaydi, boshqa har qanday matn mahsulotni "kategoriyasiz" qilib qo'yadi.
// Shuning uchun admin faqat shu ro'yxatdan TANLAYDI (select), erkin yozmaydi.
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxGalleryImages = 8;

    private readonly AppDbContext _db;
    private readonly IProductCategoryService _categoryService;

    public ProductsController(AppDbContext db, IProductCategoryService categoryService)
    {
        _db = db;
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 200);

        var query = _db.Products.Where(p => p.ApprovalStatus == "approved" && p.IsPublishedInOdoo).OrderBy(p => p.Id);
        var total = await query.CountAsync();
        var raw = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.OdooProductId,
                p.OdooTemplateId,
                p.Name,
                p.DefaultCode,
                p.Barcode,
                p.Price,
                p.CategoryName,
                p.SubcategorySlug,
                p.Brand,
                p.InStock,
                p.Rating,
                p.ReviewCount,
                HasImage = p.ImageBase64 != null,
                p.UpdatedAt
            })
            .ToListAsync();

        // categorySlug — GetCategorySlug'ni SQL'ga tarjima qilib bo'lmaydi (EF Core
        // buni qo'llab-quvvatlamaydi), shuning uchun bazadan olingan CategoryName'dan
        // xotirada hisoblanadi.
        var items = raw.Select(p => new
        {
            id = p.Id,
            odooProductId = p.OdooProductId,
            odooTemplateId = p.OdooTemplateId,
            name = p.Name,
            defaultCode = p.DefaultCode,
            barcode = p.Barcode,
            price = p.Price,
            category = p.CategoryName,
            categorySlug = _categoryService.GetCategorySlug(p.CategoryName),
            subcategorySlug = p.SubcategorySlug,
            brand = p.Brand,
            inStock = p.InStock,
            rating = p.Rating,
            reviewCount = p.ReviewCount,
            image = p.HasImage ? "/api/products/" + p.Id + "/image" : null,
            updatedAt = p.UpdatedAt
        });

        return Ok(new { items, page, total });
    }

    // Admin panel uchun — GetProducts'ning aynan o'zi, faqat IsPublishedInOdoo
    // bo'yicha filtrlanmaydi (admin "Kategoriyalar" daraxti buni ishlatadi —
    // Odoo'da vaqtincha yashirilgan, lekin baribir "approved" mahsulotni ham
    // ko'rsatishi kerak, faqat belgilab — masalan chizib — ko'rsatish uchun,
    // ochiq mijoz katalogidan farqli o'laroq mutlaqo yo'q qilib yubormasdan).
    [RequireSection("products")]
    [HttpGet("admin-list")]
    public async Task<IActionResult> GetProductsForAdminList([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 200);

        var query = _db.Products.Where(p => p.ApprovalStatus == "approved").OrderBy(p => p.Id);
        var total = await query.CountAsync();
        var raw = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.OdooProductId,
                p.OdooTemplateId,
                p.Name,
                p.DefaultCode,
                p.Barcode,
                p.Price,
                p.CategoryName,
                p.SubcategorySlug,
                p.Brand,
                p.InStock,
                p.Rating,
                p.ReviewCount,
                p.IsPublishedInOdoo,
                HasImage = p.ImageBase64 != null,
                p.UpdatedAt
            })
            .ToListAsync();

        var items = raw.Select(p => new
        {
            id = p.Id,
            odooProductId = p.OdooProductId,
            odooTemplateId = p.OdooTemplateId,
            name = p.Name,
            defaultCode = p.DefaultCode,
            barcode = p.Barcode,
            price = p.Price,
            category = p.CategoryName,
            categorySlug = _categoryService.GetCategorySlug(p.CategoryName),
            subcategorySlug = p.SubcategorySlug,
            brand = p.Brand,
            inStock = p.InStock,
            rating = p.Rating,
            reviewCount = p.ReviewCount,
            // false bo'lsa — mahsulot hozir ochiq (mijoz) katalogda ko'rinmayapti
            // (Odoo'da is_published=false), lekin "approved" holati saqlanib
            // qolgan — frontend buni chizib (strikethrough) ko'rsatishi kerak.
            isPublishedInOdoo = p.IsPublishedInOdoo,
            image = p.HasImage ? "/api/products/" + p.Id + "/image" : null,
            updatedAt = p.UpdatedAt
        });

        return Ok(new { items, page, total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var p = await _db.Products
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(p => p.Id == id && p.ApprovalStatus == "approved" && p.IsPublishedInOdoo);

        if (p == null) return NotFound(new { message = "Mahsulot topilmadi." });

        return Ok(new
        {
            id = p.Id,
            odooProductId = p.OdooProductId,
            odooTemplateId = p.OdooTemplateId,
            name = p.Name,
            defaultCode = p.DefaultCode,
            barcode = p.Barcode,
            price = p.Price,
            category = p.CategoryName,
            categorySlug = _categoryService.GetCategorySlug(p.CategoryName),
            subcategorySlug = p.SubcategorySlug,
            brand = p.Brand,
            inStock = p.InStock,
            rating = p.Rating,
            reviewCount = p.ReviewCount,
            nameOverridden = p.NameOverridden,
            categoryNameOverridden = p.CategoryNameOverridden,
            image = p.ImageBase64 != null ? "/api/products/" + p.Id + "/image" : null,
            descriptionRu = p.DescriptionRu,
            descriptionUz = p.DescriptionUz,
            images = p.Images.OrderBy(i => i.Order).Select(i => new { id = i.Id, url = "/api/products/" + p.Id + "/images/" + i.Id }),
            specifications = p.Specifications.OrderBy(s => s.Order).Select(s => new { keyRu = s.KeyRu, keyUz = s.KeyUz, valueRu = s.ValueRu, valueUz = s.ValueUz }),
            updatedAt = p.UpdatedAt
        });
    }

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
    // uchun UpdateProductDetails endi kafolatlangan to'g'ri Subcategory.Slug'ni
    // saqlay oladi (pastda, SubcategorySlug).
    [HttpGet("category-options")]
    public async Task<IActionResult> GetCategoryOptions()
    {
        return Ok(await _categoryService.GetCategoryOptionsAsync());
    }

    // Odoo'dan yangi kelgan, admin hali tasdiqlamagan mahsulotlar ro'yxati —
    // ochiq katalogda (GET /api/Products) ko'rinmaydi, faqat shu yerda ko'rinadi.
    [RequireSection("products")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingProducts()
    {
        var raw = await _db.Products
            .Where(p => p.ApprovalStatus == "pending")
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.OdooProductId,
                p.Name,
                p.DefaultCode,
                p.Price,
                p.CategoryName,
                p.SubcategorySlug,
                p.Brand,
                p.InStock,
                p.IsPublishedInOdoo,
                HasImage = p.ImageBase64 != null,
                p.CreatedAt
            })
            .ToListAsync();

        var items = raw.Select(p => new
        {
            id = p.Id,
            odooProductId = p.OdooProductId,
            name = p.Name,
            defaultCode = p.DefaultCode,
            price = p.Price,
            category = p.CategoryName,
            categorySlug = _categoryService.GetCategorySlug(p.CategoryName),
            subcategorySlug = p.SubcategorySlug,
            brand = p.Brand,
            inStock = p.InStock,
            // Odoo'da endi is_published=false bo'lib qolgan bo'lsa — admin buni
            // tasdiqlay olmaydi (SetApprovalStatus shu yerda bloklaydi). Frontend
            // buni "arxiv"/nofaol qilib ko'rsatishi uchun.
            isPublishedInOdoo = p.IsPublishedInOdoo,
            image = p.HasImage ? "/api/products/" + p.Id + "/image" : null,
            createdAt = p.CreatedAt
        });

        return Ok(items);
    }

    // Admin panel uchun — GetProduct'ning aynan o'zi, faqat "approved" filtri yo'q.
    // Shu orqali admin "pending" (hali tasdiqlanmagan) mahsulotni ham to'liq ko'rib,
    // tahrirlash formasini to'ldirishi mumkin (ochiq GET /{id} bunday mahsulotlar
    // uchun 404 qaytaradi — mijozlarga hali ko'rinmasligi kerak).
    [RequireSection("products")]
    [HttpGet("{id:int}/admin")]
    public async Task<IActionResult> GetProductForAdmin(int id)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        var (odooCategoryTop, categoryLeaf) = _categoryService.ParseCategoryPath(product.CategoryName);
        // Odoo'ning ichki nomini ("Muhandislik tizimlari") mijoz ko'radigan nomga
        // ("Santexnika") o'giramiz — admin category-options'dagi bilan bir xilini ko'radi.
        var categoryTop = _categoryService.ToDisplayCategory(odooCategoryTop);

        return Ok(new
        {
            id = product.Id,
            odooProductId = product.OdooProductId,
            odooTemplateId = product.OdooTemplateId,
            name = product.Name,
            defaultCode = product.DefaultCode,
            barcode = product.Barcode,
            price = product.Price,
            category = product.CategoryName,
            categorySlug = _categoryService.GetCategorySlug(product.CategoryName),
            subcategorySlug = product.SubcategorySlug,
            // Admin edit formasi uchun tayyor bo'laklab berilgan qiymatlar —
            // frontend qayta parse qilishi shart emas (category-options
            // ro'yxatidan qaysi biri hozir tanlanganini shu bilan bilib oladi).
            categoryTop,
            categorySubcategory = categoryLeaf,
            brand = product.Brand,
            inStock = product.InStock,
            rating = product.Rating,
            reviewCount = product.ReviewCount,
            nameOverridden = product.NameOverridden,
            categoryNameOverridden = product.CategoryNameOverridden,
            // Odoo'dagi asl qiymatlar — admin tahriri bo'lsa ham, "orqa fonda"
            // hech qachon yo'qolmaydi (ProductSyncBackgroundService har doim yangilaydi).
            odooOriginalName = product.OdooOriginalName,
            odooOriginalCategoryName = product.OdooOriginalCategoryName,
            approvalStatus = product.ApprovalStatus,
            // "approved" bo'lsa ham, agar Odoo'da is_published o'chirilgan bo'lsa,
            // mahsulot hozir ochiq katalogda ko'RINMAYDI — admin buni shu yerdan bilib olsin.
            isPublishedInOdoo = product.IsPublishedInOdoo,
            image = product.ImageBase64 != null ? "/api/products/" + product.Id + "/image" : null,
            descriptionRu = product.DescriptionRu,
            descriptionUz = product.DescriptionUz,
            images = product.Images.OrderBy(i => i.Order).Select(i => new { id = i.Id, url = "/api/products/" + product.Id + "/images/" + i.Id }),
            specifications = product.Specifications.OrderBy(s => s.Order).Select(s => new { keyRu = s.KeyRu, keyUz = s.KeyUz, valueRu = s.ValueRu, valueUz = s.ValueUz }),
            updatedAt = product.UpdatedAt
        });
    }

    // Admin yangi mahsulotni tasdiqlaydi (ochiq katalogda ko'rinadi) yoki rad etadi
    // (yashirin qoladi). Keyinchalik istalgan vaqt qayta o'zgartirish mumkin.
    [RequireSection("products")]
    [HttpPatch("{id:int}/approval")]
    public async Task<IActionResult> SetApprovalStatus(int id, ProductApprovalDto dto)
    {
        if (dto.Status != "approved" && dto.Status != "rejected")
        {
            return BadRequest(new { message = "Holat \"approved\" yoki \"rejected\" bo'lishi kerak." });
        }

        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        // Odoo'da is_published=false bo'lib qolgan mahsulotni tasdiqlab (production'ga
        // chiqarib) bo'lmaydi — chunki u hozir Odoo'ning o'zida "nashr etilmagan".
        // Rad etish (rejected) esa doim mumkin.
        if (dto.Status == "approved" && !product.IsPublishedInOdoo)
        {
            return BadRequest(new { message = "Bu mahsulot hozir Odoo'da nashr etilmagan (is_published=false) — tasdiqlab bo'lmaydi." });
        }

        product.ApprovalStatus = dto.Status;
        await _db.SaveChangesAsync();

        return Ok(new { product.Id, product.ApprovalStatus });
    }

    // Tavsif ("Qanday ishlatiladi") — Odoo'da bu ma'lumot yo'q, faqat admin
    // panel orqali qo'lda kiritiladi. Sync bu maydonga tegmaydi.
    [RequireSection("products")]
    [HttpPatch("{id:int}/description")]
    public async Task<IActionResult> UpdateProductDescription(int id, UpdateProductDescriptionDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        product.DescriptionRu = dto.DescriptionRu;
        product.DescriptionUz = dto.DescriptionUz;
        await _db.SaveChangesAsync();

        return Ok(new { descriptionRu = product.DescriptionRu, descriptionUz = product.DescriptionUz });
    }

    // Nomi — Odoo'dan sinxronlanadi, lekin admin qo'lda to'g'irlashi mumkin
    // (masalan Odoo'dagi nom noqulay/xato bo'lsa). Bir marta tahrirlansa,
    // keyingi Odoo sinxronizatsiyalari bu maydonga endi tegmaydi (NameOverridden —
    // ProductSyncBackgroundService shunga qaraydi). Narx BU YERDA YO'Q — har doim
    // faqat Odoo'dan keladi. Kategoriya (Category+Subcategory) — ERKIN MATN emas,
    // Category faqat CategoryOptions ro'yxatidagi qiymatlardan biri bo'lishi shart
    // (aks holda BadRequest) — shundagina frontend to'g'ri taniydi.
    [RequireSection("products")]
    [HttpPatch("{id:int}/details")]
    public async Task<IActionResult> UpdateProductDetails(int id, UpdateProductDetailsDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        if (dto.Name != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "Nomi bo'sh bo'lishi mumkin emas." });
            }
            product.Name = dto.Name;
            product.NameOverridden = true;
        }

        // MUHIM (tuzatildi): avval bu blok FAQAT dto.Category berilganda ishga
        // tushardi. Agar admin faqat Subkategoriyani o'zgartirsa (Kategoriya
        // allaqachon to'g'ri bo'lgani uchun frontend uni "o'zgarmagan" deb
        // umuman yubormasa) — Subkategoriya ham butunlay e'tiborsiz qoldirilar
        // edi (mahsulot "Ichki kategoriyasiz"da qolib ketardi). Endi
        // dto.Subcategory yolg'iz kelsa ham, mahsulotning HOZIRGI kategoriyasi
        // asos qilib olinadi — validatsiya/moslashtirish mantig'i endi
        // IProductCategoryService'da (q. Services/ProductCategoryService.cs).
        if (dto.Category != null || dto.Subcategory != null)
        {
            var resolution = await _categoryService.ResolveCategoryChangeAsync(product.CategoryName, dto.Category, dto.Subcategory);
            if (!resolution.Success)
            {
                return BadRequest(new { message = resolution.ErrorMessage });
            }

            product.CategoryName = resolution.CategoryName;
            product.CategoryNameOverridden = true;
            product.SubcategorySlug = resolution.SubcategorySlug;
        }

        await _db.SaveChangesAsync();

        return Ok(new { product.Id, product.Name, product.CategoryName });
    }

    // Xususiyatlar jadvali (masalan "Akkumulyator" -> "18 V Li-Ion") — butun
    // ro'yxat bir yo'la almashtiriladi (admin panel formasi shu tarzda saqlaydi).
    [RequireSection("products")]
    [HttpPut("{id:int}/specifications")]
    public async Task<IActionResult> ReplaceProductSpecifications(int id, List<ProductSpecificationDto> specifications)
    {
        var product = await _db.Products
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        _db.ProductSpecifications.RemoveRange(product.Specifications);
        product.Specifications = specifications.Select((s, index) => new ProductSpecification
        {
            KeyRu = s.KeyRu,
            KeyUz = s.KeyUz,
            ValueRu = s.ValueRu,
            ValueUz = s.ValueUz,
            Order = index
        }).ToList();

        await _db.SaveChangesAsync();

        return Ok(product.Specifications.Select(s => new { keyRu = s.KeyRu, keyUz = s.KeyUz, valueRu = s.ValueRu, valueUz = s.ValueUz }));
    }

    // Galereya rasmi — asosiy rasmdan (POST /{id}/image) tashqari qo'shimcha
    // rasmlar. Ro'yxat/detal javobida katta base64 yubormaslik uchun alohida
    // endpoint orqali beriladi (asosiy rasm bilan bir xil mantiq).
    [HttpGet("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> GetProductGalleryImage(int id, int imageId)
    {
        var base64 = await _db.ProductImages
            .Where(i => i.Id == imageId && i.ProductId == id)
            .Select(i => i.ImageBase64)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(base64)) return NotFound();

        if (!TryDecodeImage(base64, out var bytes, out var contentType)) return NotFound();

        return File(bytes, contentType);
    }

    [RequireSection("products")]
    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> AddProductGalleryImage(int id, IFormFile file)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        if (product.Images.Count >= MaxGalleryImages)
        {
            return BadRequest(new { message = $"Bitta mahsulotga ko'pi bilan {MaxGalleryImages} ta rasm qo'shish mumkin." });
        }

        if (!TryReadValidatedImage(file, out var base64, out var error))
        {
            return BadRequest(new { message = error });
        }

        var image = new ProductImage
        {
            ProductId = id,
            ImageBase64 = base64,
            Order = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.Order) + 1
        };
        _db.ProductImages.Add(image);
        await _db.SaveChangesAsync();

        return Ok(new { id = image.Id, url = $"/api/products/{id}/images/{image.Id}" });
    }

    [RequireSection("products")]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteProductGalleryImage(int id, int imageId)
    {
        var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id);
        if (image == null) return NotFound(new { message = "Rasm topilmadi." });

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rasm o'chirildi." });
    }

    // Odoo'dan olingan rasm — ro'yxat/detal javobida katta base64 yubormaslik uchun
    // alohida endpoint. Rasm yo'q bo'lsa 404.
    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetProductImage(int id)
    {
        var base64 = await _db.Products
            .Where(p => p.Id == id)
            .Select(p => p.ImageBase64)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(base64)) return NotFound();
        if (!TryDecodeImage(base64, out var bytes, out var contentType)) return NotFound();

        return File(bytes, contentType);
    }

    // Admin panel (Superuser "products" ruxsati bilan ham) rasm yuklaydi/o'zgartiradi.
    // multipart/form-data, "file" maydoni.
    [RequireSection("products")]
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> UploadProductImage(int id, IFormFile file)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        if (!TryReadValidatedImage(file, out var base64, out var error))
        {
            return BadRequest(new { message = error });
        }

        product.ImageBase64 = base64;
        await _db.SaveChangesAsync();

        return Ok(new { image = $"/api/products/{id}/image" });
    }

    [RequireSection("products")]
    [HttpDelete("{id:int}/image")]
    public async Task<IActionResult> DeleteProductImage(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        product.ImageBase64 = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rasm o'chirildi." });
    }

    // Fayldan baytlarni o'qiydi, hajm va haqiqiy rasm formatini (magic byte
    // orqali, fayl kengaytmasiga ishonmasdan) tekshiradi.
    private static bool TryReadValidatedImage(IFormFile? file, out string base64, out string error)
    {
        base64 = string.Empty;
        error = string.Empty;

        if (file == null || file.Length == 0)
        {
            error = "Rasm fayli yuborilmadi.";
            return false;
        }

        if (file.Length > MaxImageBytes)
        {
            error = "Rasm hajmi 5 MB dan oshmasligi kerak.";
            return false;
        }

        using var ms = new MemoryStream();
        file.CopyTo(ms);
        var bytes = ms.ToArray();

        if (!IsSupportedImage(bytes))
        {
            error = "Faqat JPEG, PNG yoki WebP formatidagi rasm qabul qilinadi.";
            return false;
        }

        base64 = Convert.ToBase64String(bytes);
        return true;
    }

    private static bool IsSupportedImage(byte[] bytes)
    {
        var isJpeg = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var isPng = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        var isWebp = bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
        return isJpeg || isPng || isWebp;
    }

    private static bool TryDecodeImage(string base64, out byte[] bytes, out string contentType)
    {
        contentType = "image/png";
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }

        // Format aniq saqlanmaydi — magic byte orqali JPEG/PNG/WebP farqlaymiz.
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            contentType = "image/jpeg";
        }
        else if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
        {
            contentType = "image/webp";
        }

        return true;
    }
}
