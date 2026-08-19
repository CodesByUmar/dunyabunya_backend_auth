using AuthApi.Data;
using AuthApi.Filters;
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
                updatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        return Ok(product);
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

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return NotFound();
        }

        // Format aniq saqlanmaydi — magic byte orqali JPEG/PNG/WebP farqlaymiz.
        string contentType;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            contentType = "image/jpeg";
        }
        else if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
        {
            contentType = "image/webp";
        }
        else
        {
            contentType = "image/png";
        }

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

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Rasm fayli yuborilmadi." });
        }

        if (file.Length > MaxImageBytes)
        {
            return BadRequest(new { message = "Rasm hajmi 5 MB dan oshmasligi kerak." });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Haqiqiy rasm ekanini magic byte orqali tekshiramiz (fayl kengaytmasiga ishonib bo'lmaydi).
        var isJpeg = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var isPng = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        var isWebp = bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

        if (!isJpeg && !isPng && !isWebp)
        {
            return BadRequest(new { message = "Faqat JPEG, PNG yoki WebP formatidagi rasm qabul qilinadi." });
        }

        product.ImageBase64 = Convert.ToBase64String(bytes);
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
}
