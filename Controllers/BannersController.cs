using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Bosh sahifa bannerlari. O'qish — ochiq. Yozish — Admin yoki "banners"
// ruxsati berilgan Superuser (admin/src/lib/session.ts'dagi SUPERUSER_GRANTABLE'ga mos).
[ApiController]
[Route("api/[controller]")]
public class BannersController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    // Kategoriyalardagi kabi — rasm bazaga (base64) emas, diskka saqlanadi.
    private static readonly string UploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads", "banners");

    private readonly AppDbContext _db;
    public BannersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.Banners.AsQueryable();
        if (activeOnly) query = query.Where(b => b.IsActive);
        return Ok(await query.OrderBy(b => b.Order).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Banners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Banner topilmadi." });
        return Ok(entity);
    }

    [RequireSection("banners")]
    [HttpPost]
    public async Task<IActionResult> Create(BannerDto dto)
    {
        var entity = MapToEntity(dto);
        _db.Banners.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("banners")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BannerDto dto)
    {
        var entity = await _db.Banners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Banner topilmadi." });

        entity.Position = dto.Position;
        entity.Order = dto.Order;
        entity.IsActive = dto.IsActive;
        entity.TagRu = dto.TagRu;
        entity.TagUz = dto.TagUz;
        entity.TitleRu = dto.TitleRu;
        entity.TitleUz = dto.TitleUz;
        entity.SubtitleRu = dto.SubtitleRu;
        entity.SubtitleUz = dto.SubtitleUz;
        entity.ButtonTextRu = dto.ButtonTextRu;
        entity.ButtonTextUz = dto.ButtonTextUz;
        entity.Image = dto.Image;
        entity.LinkType = dto.LinkType;
        entity.PagePath = dto.PagePath;
        entity.CategorySlug = dto.CategorySlug;
        entity.SubcategorySlug = dto.SubcategorySlug;
        entity.AccentMode = dto.AccentMode;
        entity.Accent = dto.Accent;
        entity.CustomAccent = dto.CustomAccent;
        entity.OverlayOpacity = dto.OverlayOpacity;
        entity.TextAlign = dto.TextAlign;
        entity.ImagePosition = dto.ImagePosition;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("banners")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Banners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Banner topilmadi." });
        _db.Banners.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var exists = await _db.Banners.AnyAsync(b => b.Id == id);
        if (!exists) return NotFound();

        var path = ImagePath(id);
        if (!System.IO.File.Exists(path)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return new FileContentResult(bytes, DetectContentType(bytes));
    }

    [RequireSection("banners")]
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        var entity = await _db.Banners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Banner topilmadi." });

        if (!TryReadValidatedImage(file, out var bytes, out var error))
        {
            return BadRequest(new { message = error });
        }

        Directory.CreateDirectory(UploadsRoot);
        await System.IO.File.WriteAllBytesAsync(ImagePath(id), bytes);

        entity.Image = $"/api/banners/{id}/image";
        await _db.SaveChangesAsync();

        return Ok(new { url = entity.Image });
    }

    [RequireSection("banners")]
    [HttpDelete("{id:int}/image")]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var entity = await _db.Banners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Banner topilmadi." });

        var path = ImagePath(id);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        entity.Image = string.Empty;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rasm o'chirildi." });
    }

    private static string ImagePath(int id) => Path.Combine(UploadsRoot, $"banner_{id}");

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

    private static Banner MapToEntity(BannerDto dto) => new()
    {
        Position = dto.Position,
        Order = dto.Order,
        IsActive = dto.IsActive,
        TagRu = dto.TagRu,
        TagUz = dto.TagUz,
        TitleRu = dto.TitleRu,
        TitleUz = dto.TitleUz,
        SubtitleRu = dto.SubtitleRu,
        SubtitleUz = dto.SubtitleUz,
        ButtonTextRu = dto.ButtonTextRu,
        ButtonTextUz = dto.ButtonTextUz,
        Image = dto.Image,
        LinkType = dto.LinkType,
        PagePath = dto.PagePath,
        CategorySlug = dto.CategorySlug,
        SubcategorySlug = dto.SubcategorySlug,
        AccentMode = dto.AccentMode,
        Accent = dto.Accent,
        CustomAccent = dto.CustomAccent,
        OverlayOpacity = dto.OverlayOpacity,
        TextAlign = dto.TextAlign,
        ImagePosition = dto.ImagePosition
    };
}
