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
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    // Rasm fayllari bazaga emas, diskka saqlanadi (mahsulotlardan farqli) —
    // bazada faqat "/api/categories/.../image" yo'li turadi. Fayl nomi
    // kengaytmasiz (masalan "category_5") — qayta yuklaganda eski format
    // qanday bo'lishidan qat'iy nazar bir xil joyga yozilaveradi, formatni esa
    // GET so'ralganda magic byte orqali aniqlaymiz (Products'dagi kabi).
    private static readonly string UploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads", "categories");

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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _db.Categories
            .Where(c => c.Id == id)
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
            .FirstOrDefaultAsync();

        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });
        return Ok(category);
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

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetCategoryImage(int id)
    {
        var exists = await _db.Categories.AnyAsync(c => c.Id == id);
        if (!exists) return NotFound();

        return ServeImageFile(CategoryImagePath(id));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> UploadCategoryImage(int id, IFormFile file)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });

        if (!TryReadValidatedImage(file, out var bytes, out var error))
        {
            return BadRequest(new { message = error });
        }

        Directory.CreateDirectory(UploadsRoot);
        await System.IO.File.WriteAllBytesAsync(CategoryImagePath(id), bytes);

        category.Image = $"/api/categories/{id}/image";
        await _db.SaveChangesAsync();

        return Ok(new { url = category.Image });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}/image")]
    public async Task<IActionResult> DeleteCategoryImage(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });

        DeleteFileIfExists(CategoryImagePath(id));
        category.Image = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rasm o'chirildi." });
    }

    [HttpGet("{categoryId:int}/subcategories/{subId:int}/image")]
    public async Task<IActionResult> GetSubcategoryImage(int categoryId, int subId)
    {
        var exists = await _db.Subcategories.AnyAsync(s => s.Id == subId && s.CategoryId == categoryId);
        if (!exists) return NotFound();

        return ServeImageFile(SubcategoryImagePath(subId));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{categoryId:int}/subcategories/{subId:int}/image")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> UploadSubcategoryImage(int categoryId, int subId, IFormFile file)
    {
        var subcategory = await _db.Subcategories.FirstOrDefaultAsync(s => s.Id == subId && s.CategoryId == categoryId);
        if (subcategory == null) return NotFound(new { message = "Subkategoriya topilmadi." });

        if (!TryReadValidatedImage(file, out var bytes, out var error))
        {
            return BadRequest(new { message = error });
        }

        Directory.CreateDirectory(UploadsRoot);
        await System.IO.File.WriteAllBytesAsync(SubcategoryImagePath(subId), bytes);

        subcategory.Image = $"/api/categories/{categoryId}/subcategories/{subId}/image";
        await _db.SaveChangesAsync();

        return Ok(new { url = subcategory.Image });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{categoryId:int}/subcategories/{subId:int}/image")]
    public async Task<IActionResult> DeleteSubcategoryImage(int categoryId, int subId)
    {
        var subcategory = await _db.Subcategories.FirstOrDefaultAsync(s => s.Id == subId && s.CategoryId == categoryId);
        if (subcategory == null) return NotFound(new { message = "Subkategoriya topilmadi." });

        DeleteFileIfExists(SubcategoryImagePath(subId));
        subcategory.Image = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rasm o'chirildi." });
    }

    private static string CategoryImagePath(int id) => Path.Combine(UploadsRoot, $"category_{id}");
    private static string SubcategoryImagePath(int id) => Path.Combine(UploadsRoot, $"subcategory_{id}");

    private static void DeleteFileIfExists(string path)
    {
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private static IActionResult ServeImageFile(string path)
    {
        if (!System.IO.File.Exists(path)) return new NotFoundResult();

        var bytes = System.IO.File.ReadAllBytes(path);
        return new FileContentResult(bytes, DetectContentType(bytes));
    }

    private static bool TryReadValidatedImage(IFormFile? file, out byte[] bytes, out string error)
    {
        bytes = [];
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
        bytes = ms.ToArray();

        var isJpeg = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var isPng = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        var isWebp = bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

        if (!isJpeg && !isPng && !isWebp)
        {
            error = "Faqat JPEG, PNG yoki WebP formatidagi rasm qabul qilinadi.";
            return false;
        }

        return true;
    }

    private static string DetectContentType(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return "image/webp";
        return "image/png";
    }
}
