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
    private readonly AppDbContext _db;
    public BannersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.Banners.AsQueryable();
        if (activeOnly) query = query.Where(b => b.IsActive);
        return Ok(await query.OrderBy(b => b.Order).ToListAsync());
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
