using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Services;
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
                c.NameRu,
                c.Slug,
                c.Image,
                c.Order,
                Subcategories = c.Subcategories.OrderBy(s => s.Order).Select(s => new
                {
                    s.Id,
                    s.NameRu,
                    s.Slug,
                    s.Image,
                    s.Order
                }).ToList()
            })
            .ToListAsync();

        var keys = categories
            .Select(c => $"data.categories.{c.Slug}")
            .Concat(categories.SelectMany(c => c.Subcategories).Select(s => $"data.subcategories.{s.Slug}"))
            .ToList();
        var uzByKey = await GetUzTranslationsAsync(keys);

        var result = categories.Select(c => new
        {
            c.Id,
            c.NameRu,
            NameUz = uzByKey.GetValueOrDefault($"data.categories.{c.Slug}", c.NameRu),
            c.Slug,
            c.Image,
            c.Order,
            Subcategories = c.Subcategories.Select(s => new
            {
                s.Id,
                s.NameRu,
                NameUz = uzByKey.GetValueOrDefault($"data.subcategories.{s.Slug}", s.NameRu),
                s.Slug,
                s.Image,
                s.Order
            })
        });

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _db.Categories
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.NameRu,
                c.Slug,
                c.Image,
                c.Order,
                Subcategories = c.Subcategories.OrderBy(s => s.Order).Select(s => new
                {
                    s.Id,
                    s.NameRu,
                    s.Slug,
                    s.Image,
                    s.Order
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (category == null) return NotFound(new { message = "Kategoriya topilmadi." });

        var keys = new List<string> { $"data.categories.{category.Slug}" }
            .Concat(category.Subcategories.Select(s => $"data.subcategories.{s.Slug}"))
            .ToList();
        var uzByKey = await GetUzTranslationsAsync(keys);

        return Ok(new
        {
            category.Id,
            category.NameRu,
            NameUz = uzByKey.GetValueOrDefault($"data.categories.{category.Slug}", category.NameRu),
            category.Slug,
            category.Image,
            category.Order,
            Subcategories = category.Subcategories.Select(s => new
            {
                s.Id,
                s.NameRu,
                NameUz = uzByKey.GetValueOrDefault($"data.subcategories.{s.Slug}", s.NameRu),
                s.Slug,
                s.Image,
                s.Order
            })
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCategory(CategoryDto dto)
    {
        var existingCategorySlugs = await _db.Categories.Select(c => c.Slug).ToHashSetAsync();
        var categorySlug = CategorySlugHelper.MakeUnique(CategorySlugHelper.Slugify(dto.NameRu), existingCategorySlugs);

        var subSlugsUsed = new HashSet<string>();
        var subDtos = dto.Subcategories ?? new();
        var subSlugs = new List<string>();
        var category = new Category
        {
            NameRu = dto.NameRu,
            Slug = categorySlug,
            Image = dto.Image,
            Order = dto.Order,
            Subcategories = subDtos.Select(s =>
            {
                var subSlug = CategorySlugHelper.MakeUnique(CategorySlugHelper.Slugify(s.NameRu), subSlugsUsed);
                subSlugsUsed.Add(subSlug);
                subSlugs.Add(subSlug);
                return new Subcategory
                {
                    NameRu = s.NameRu,
                    Slug = subSlug,
                    Image = s.Image,
                    Order = s.Order
                };
            }).ToList()
        };

        _db.Categories.Add(category);

        await UpsertNameTranslationAsync($"data.categories.{categorySlug}", dto.NameRu, dto.NameUz);
        for (var i = 0; i < subDtos.Count; i++)
        {
            await UpsertNameTranslationAsync($"data.subcategories.{subSlugs[i]}", subDtos[i].NameRu, subDtos[i].NameUz);
        }

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

        // Slug'ga TEGILMAYDI — mavjud havolalar (mahsulot/banner CategorySlug/
        // SubcategorySlug) buzilmasligi uchun, faqat yaratishda bir marta o'rnatiladi.
        category.NameRu = dto.NameRu;
        category.Image = dto.Image;
        category.Order = dto.Order;
        await UpsertNameTranslationAsync($"data.categories.{category.Slug}", dto.NameRu, dto.NameUz);

        if (dto.Subcategories != null)
        {
            // MUHIM: eski (Id) mos kelgan subkategoriyalar joyida YANGILANADI, o'chirib
            // qayta yaratilmaydi — aks holda yuklangan rasm (diskda subId bo'yicha
            // saqlanadi) va Slug (URL) har bir tahrirda buzilib ketardi.
            var existingById = category.Subcategories.ToDictionary(s => s.Id);
            var subSlugsUsed = category.Subcategories.Select(s => s.Slug).ToHashSet();
            var keptIds = new HashSet<int>();

            foreach (var s in dto.Subcategories)
            {
                if (s.Id.HasValue && existingById.TryGetValue(s.Id.Value, out var existing))
                {
                    existing.NameRu = s.NameRu;
                    existing.Image = s.Image;
                    existing.Order = s.Order;
                    // Slug'ga tegilmaydi — mavjud URL saqlanib qoladi.
                    keptIds.Add(existing.Id);
                    await UpsertNameTranslationAsync($"data.subcategories.{existing.Slug}", s.NameRu, s.NameUz);
                }
                else
                {
                    var subSlug = CategorySlugHelper.MakeUnique(CategorySlugHelper.Slugify(s.NameRu), subSlugsUsed);
                    subSlugsUsed.Add(subSlug);
                    await UpsertNameTranslationAsync($"data.subcategories.{subSlug}", s.NameRu, s.NameUz);
                    category.Subcategories.Add(new Subcategory
                    {
                        NameRu = s.NameRu,
                        Slug = subSlug,
                        Image = s.Image,
                        Order = s.Order
                    });
                }
            }

            var removed = category.Subcategories.Where(s => s.Id != 0 && !keptIds.Contains(s.Id)).ToList();
            if (removed.Count > 0) _db.Subcategories.RemoveRange(removed);
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

    // "data.categories.{slug}" / "data.subcategories.{slug}" kalitlari bo'yicha
    // Translations jadvalidan o'zbekcha matnlarni bittada (N+1 emas) o'qiydi.
    private async Task<Dictionary<string, string>> GetUzTranslationsAsync(List<string> keys)
    {
        if (keys.Count == 0) return new Dictionary<string, string>();

        return await _db.Translations
            .Where(t => t.App == "user" && keys.Contains(t.Key))
            .ToDictionaryAsync(t => t.Key, t => t.Uz);
    }

    // Kategoriya/subkategoriya saqlanganda, uning tarjimasini ham (bor bo'lsa
    // yangilaydi, yo'q bo'lsa yaratadi) Translations jadvaliga yozadi — shu orqali
    // "Tarjimalar" sahifasi va kategoriya tahrirlash formasi BITTA manbadan
    // ishlaydi (bir-biridan uzilib qolmaydi).
    private async Task UpsertNameTranslationAsync(string key, string ru, string? uz)
    {
        if (string.IsNullOrWhiteSpace(uz)) return;

        var existing = await _db.Translations.FirstOrDefaultAsync(t => t.App == "user" && t.Key == key);
        if (existing != null)
        {
            existing.Ru = ru;
            existing.Uz = uz;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Translations.Add(new Translation { App = "user", Key = key, Ru = ru, Uz = uz });
        }
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
