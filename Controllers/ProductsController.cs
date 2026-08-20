using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Frontend uchun — Odoo'dan sinxronlangan (is_published=true) mahsulotlar.
// Ochiq (public), autentifikatsiya talab qilinmaydi — katalog hammaga ko'rinadi.
//
// MUHIM: mahsulotning nom/narx/brend/ombor kabi barcha maydonlari Odoo'dan
// har daqiqada avtomatik qayta yoziladi (ProductSyncBackgroundService) —
// shuning uchun bu yerda ularni o'zgartiradigan endpoint YO'Q (qo'lda
// o'zgartirilgan bo'lsa ham, bir daqiqada Odoo qiymati bilan almashtirilib
// ketardi). Faqat RASM — sync bu maydonga tegmaydi, shuning uchun admin
// panel orqali xavfsiz boshqarish mumkin.
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxGalleryImages = 8;

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

        var query = _db.Products.OrderBy(p => p.Id);
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
            .Where(p => p.Id == id)
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
                description = p.Description,
                images = p.Images.OrderBy(i => i.Order).Select(i => new { id = i.Id, url = "/api/products/" + p.Id + "/images/" + i.Id }),
                specifications = p.Specifications.OrderBy(s => s.Order).Select(s => new { key = s.Key, value = s.Value }),
                updatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        return Ok(product);
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
