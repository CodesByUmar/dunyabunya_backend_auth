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
                odooTemplateId = p.OdooTemplateId,
                name = p.Name,
                defaultCode = p.DefaultCode,
                barcode = p.Barcode,
                price = p.Price,
                cost = p.Cost,
                categoryName = p.CategoryName,
                brand = p.Brand,
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
                odooTemplateId = p.OdooTemplateId,
                name = p.Name,
                defaultCode = p.DefaultCode,
                barcode = p.Barcode,
                price = p.Price,
                cost = p.Cost,
                categoryName = p.CategoryName,
                brand = p.Brand,
                updatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (product == null) return NotFound(new { message = "Mahsulot topilmadi." });

        return Ok(product);
    }
}
