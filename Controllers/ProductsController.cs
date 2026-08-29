using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
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

    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 200);

        var query = _db.Products.Where(p => p.ApprovalStatus == "approved").OrderBy(p => p.Id);
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new
            {
                id = p.Id,
                odooProductId = p.OdooProductId,
                odooTemplateId = p.OdooTemplateId,
                name = p.Name,
                defaultCode = p.DefaultCode,
                barcode = p.Barcode,
                price = p.Price,
                category = p.CategoryName,
                brand = p.Brand,
                inStock = p.InStock,
                rating = p.Rating,
                reviewCount = p.ReviewCount,
                image = p.ImageBase64 != null ? "/api/products/" + p.Id + "/image" : null,
                updatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items, page, total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var product = await _db.Products
            .Where(p => p.Id == id && p.ApprovalStatus == "approved")
            .Select(p => new
            {
                id = p.Id,
                odooProductId = p.OdooProductId,
                odooTemplateId = p.OdooTemplateId,
                name = p.Name,
                defaultCode = p.DefaultCode,
                barcode = p.Barcode,
                price = p.Price,
                category = p.CategoryName,
                brand = p.Brand,
                inStock = p.InStock,
                rating = p.Rating,
                reviewCount = p.ReviewCount,
                nameOverridden = p.NameOverridden,
                categoryNameOverridden = p.CategoryNameOverridden,
                image = p.ImageBase64 != null ? "/api/products/" + p.Id + "/image" : null,
                description = p.Description,
                images = p.Images.OrderBy(i => i.Order).Select(i => new { id = i.Id, url = "/api/products/" + p.Id + "/images/" + i.Id }),
                specifications = p.Specifications.OrderBy(s => s.Order).Select(s => new { key = s.Key, value = s.Value }),
                updatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        return Ok(product);
    }

    // Admin panelda "Kategoriya" va "Subkategoriya"ni SELECT orqali tanlash uchun —
    // ikkalasi ham erkin matn EMAS. Kategoriya sifatida MIJOZ ko'radigan nom
    // qaytariladi (masalan "Santexnika"), Odoo'ning ichki nomi ("Muhandislik
    // tizimlari") admin'ga umuman ko'rsatilmaydi — shunday bo'lmasa admin
    // saytdagi qaysi bo'limga tegishli ekanini bilolmay chalkashadi. Subkategoriya —
    // har bir kategoriya ostida Odoo'dan haqiqatda kelgan (Products jadvalida
    // mavjud) qiymatlar — shunda admin hech qachon "hech qanday mahsulotda yo'q"
    // subkategoriya kiritolmaydi.
    [HttpGet("category-options")]
    public async Task<IActionResult> GetCategoryOptions()
    {
        var allCategoryNames = await _db.Products
            .Where(p => p.CategoryName != null)
            .Select(p => p.CategoryName!)
            .Distinct()
            .ToListAsync();

        var subcategoriesByOdooTop = new Dictionary<string, SortedSet<string>>();
        foreach (var odooTop in CategoryDisplayToOdoo.Values) subcategoriesByOdooTop[odooTop] = new SortedSet<string>();

        foreach (var raw in allCategoryNames)
        {
            var (top, leaf) = ParseCategoryPath(raw);
            if (top != null && leaf != null && subcategoriesByOdooTop.ContainsKey(top))
            {
                subcategoriesByOdooTop[top].Add(leaf);
            }
        }

        var result = CategoryDisplayToOdoo.Select(kv => new
        {
            category = kv.Key,
            subcategories = subcategoriesByOdooTop[kv.Value].ToList()
        });

        return Ok(result);
    }

    // Odoo'dan yangi kelgan, admin hali tasdiqlamagan mahsulotlar ro'yxati —
    // ochiq katalogda (GET /api/Products) ko'rinmaydi, faqat shu yerda ko'rinadi.
    [RequireSection("products")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingProducts()
    {
        var items = await _db.Products
            .Where(p => p.ApprovalStatus == "pending")
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                odooProductId = p.OdooProductId,
                name = p.Name,
                defaultCode = p.DefaultCode,
                price = p.Price,
                category = p.CategoryName,
                brand = p.Brand,
                inStock = p.InStock,
                image = p.ImageBase64 != null ? "/api/products/" + p.Id + "/image" : null,
                createdAt = p.CreatedAt
            })
            .ToListAsync();

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

        var (odooCategoryTop, categoryLeaf) = ParseCategoryPath(product.CategoryName);
        // Odoo'ning ichki nomini ("Muhandislik tizimlari") mijoz ko'radigan nomga
        // ("Santexnika") o'giramiz — admin category-options'dagi bilan bir xilini ko'radi.
        var categoryTop = odooCategoryTop != null
            ? CategoryDisplayToOdoo.FirstOrDefault(kv => kv.Value == odooCategoryTop).Key
            : null;

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
            approvalStatus = product.ApprovalStatus,
            image = product.ImageBase64 != null ? "/api/products/" + product.Id + "/image" : null,
            description = product.Description,
            images = product.Images.OrderBy(i => i.Order).Select(i => new { id = i.Id, url = "/api/products/" + product.Id + "/images/" + i.Id }),
            specifications = product.Specifications.OrderBy(s => s.Order).Select(s => new { key = s.Key, value = s.Value }),
            updatedAt = product.UpdatedAt
        });
    }

    // "Hammasi / Elektrika / Past kuchlanishli uskunalar / AVR" -> ("Elektrika", "AVR").
    // Frontend'ning src/lib/api.ts:mapBackendCategoryPath bilan bir xil mantiq —
    // birinchi (Hammasi'dan keyingi) bo'lak "kategoriya", oxirgi bo'lak "subkategoriya".
    private static (string? Top, string? Leaf) ParseCategoryPath(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return (null, null);

        var segments = categoryName.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s != "Hammasi")
            .ToList();

        if (segments.Count == 0) return (null, null);
        return (segments[0], segments[^1]);
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

        product.Description = dto.Description;
        await _db.SaveChangesAsync();

        return Ok(new { description = product.Description });
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

        if (dto.Category != null)
        {
            // dto.Category — mijoz ko'radigan nom (masalan "Santexnika"). Odoo'ning
            // ichki nomiga ("Muhandislik tizimlari") shu orqali o'giriladi.
            if (!CategoryDisplayToOdoo.TryGetValue(dto.Category, out var odooCategory))
            {
                return BadRequest(new { message = $"Kategoriya faqat shulardan biri bo'lishi kerak: {string.Join(", ", CategoryDisplayToOdoo.Keys)}" });
            }

            if (string.IsNullOrWhiteSpace(dto.Subcategory))
            {
                return BadRequest(new { message = "Subkategoriya bo'sh bo'lishi mumkin emas." });
            }

            // Subkategoriya ham erkin matn EMAS — faqat shu Kategoriya ostida Odoo'dan
            // haqiqatda kelgan (boshqa mahsulotlarda mavjud) qiymatlardan biri bo'lishi
            // kerak (GET /category-options shu ro'yxatni beradi).
            var validSubcategories = await _db.Products
                .Where(p => p.CategoryName != null)
                .Select(p => p.CategoryName!)
                .Distinct()
                .ToListAsync();
            var knownForCategory = validSubcategories
                .Select(ParseCategoryPath)
                .Where(t => t.Top == odooCategory && t.Leaf != null)
                .Select(t => t.Leaf!)
                .ToHashSet();

            if (!knownForCategory.Contains(dto.Subcategory))
            {
                return BadRequest(new
                {
                    message = knownForCategory.Count > 0
                        ? $"\"{dto.Category}\" kategoriyasi uchun subkategoriya faqat shulardan biri bo'lishi kerak: {string.Join(", ", knownForCategory)}"
                        : $"\"{dto.Category}\" kategoriyasi uchun hali hech qanday ma'lum subkategoriya yo'q."
                });
            }

            // "Hammasi / {OdooCategory} / {Subcategory}" — frontend faqat birinchi va
            // oxirgi bo'lakni o'qiydi, o'rtadagi bo'lak muhim emas (q. ParseCategoryPath).
            product.CategoryName = $"Hammasi / {odooCategory} / {dto.Subcategory}";
            product.CategoryNameOverridden = true;
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
            Key = s.Key,
            Value = s.Value,
            Order = index
        }).ToList();

        await _db.SaveChangesAsync();

        return Ok(product.Specifications.Select(s => new { key = s.Key, value = s.Value }));
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
