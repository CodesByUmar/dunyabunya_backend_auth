using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthApi.Controllers;

// Mijozning sevimli mahsulotlari — avval faqat brauzerda (localStorage) saqlanardi,
// endi backend orqali qaysi qurilmadan kirmasin bir xil ro'yxat ko'rinadi.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly AppDbContext _db;

    public WishlistController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetWishlist()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var items = await _db.WishlistItems
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Join(_db.Products, w => w.ProductId, p => p.Id, (w, p) => new
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
                updatedAt = p.UpdatedAt,
                addedAt = w.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> AddToWishlist([FromBody] AddWishlistItemDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var productExists = await _db.Products.AnyAsync(p => p.Id == dto.ProductId);
        if (!productExists) return NotFound(new { message = "Mahsulot topilmadi." });

        var alreadyExists = await _db.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.ProductId == dto.ProductId);

        if (!alreadyExists)
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = userId.Value, ProductId = dto.ProductId });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Poyga holati — boshqa so'rov ayni shu paytda xuddi shu yozuvni qo'shib ulgurgan
                // bo'lishi mumkin (UNIQUE cheklov ishga tushadi); bu holat ham muvaffaqiyat hisoblanadi.
            }
        }

        return Ok(new { message = "Sevimlilarga qo'shildi." });
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> RemoveFromWishlist(int productId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

        if (item != null)
        {
            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Sevimlilardan olib tashlandi." });
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
