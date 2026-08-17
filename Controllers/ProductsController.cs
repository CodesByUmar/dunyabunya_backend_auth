using AuthApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Frontend uchun — Odoo'dan sinxronlangan (is_published=true) mahsulotlar.
// Ochiq (public), autentifikatsiya talab qilinmaydi — katalog hammaga ko'rinadi.
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
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

        // Odoo rasm formatini aniq bermaydi — magic byte orqali JPEG/PNG farqlaymiz.
        var contentType = bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8
            ? "image/jpeg"
            : "image/png";

        return File(bytes, contentType);
    }
}
