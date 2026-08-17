using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Katalog kategoriyalari — ochiq (public) o'qish, faqat Admin o'zgartira oladi.
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Categories
            .OrderBy(c => c.Order)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Image,
                c.Order,
                Subcategories = c.Subcategories.OrderBy(s => s.Order).Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Slug,
                    s.Image,
                    s.Order
                })
            })
            .ToListAsync();

        return Ok(categories);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCategory(CategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Image = dto.Image,
            Order = dto.Order,
            Subcategories = (dto.Subcategories ?? new()).Select(s => new Subcategory
            {
                Name = s.Name,
                Slug = s.Slug,
                Image = s.Image,
                Order = s.Order
            }).ToList()
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Ok(new { category.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, CategoryDto dto)
    {
        var category = await _db.Categories
            .Include(c => c.Subcategories)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });

        category.Name = dto.Name;
        category.Slug = dto.Slug;
        category.Image = dto.Image;
        category.Order = dto.Order;

        if (dto.Subcategories != null)
        {
            _db.Subcategories.RemoveRange(category.Subcategories);
            category.Subcategories = dto.Subcategories.Select(s => new Subcategory
            {
                Name = s.Name,
                Slug = s.Slug,
                Image = s.Image,
                Order = s.Order
            }).ToList();
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Yangilandi." });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }
}
