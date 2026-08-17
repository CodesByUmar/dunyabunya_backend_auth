using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Sovg'a darajalari (masalan "100 ball = sertifikat"). O'qish ochiq,
// boshqarish Admin yoki "gifts" ruxsati bor Superuser.
[ApiController]
[Route("api/[controller]")]
public class GiftTiersController : ControllerBase
{
    private readonly AppDbContext _db;
    public GiftTiersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.GiftTiers.OrderBy(t => t.Points).ToListAsync());

    [RequireSection("gifts")]
    [HttpPost]
    public async Task<IActionResult> Create(GiftTierDto dto)
    {
        var entity = Map(dto);
        _db.GiftTiers.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("gifts")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, GiftTierDto dto)
    {
        var entity = await _db.GiftTiers.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });

        entity.Points = dto.Points;
        entity.Title = dto.Title;
        entity.TitleUz = dto.TitleUz;
        entity.Description = dto.Description;
        entity.DescriptionUz = dto.DescriptionUz;
        entity.Image = dto.Image;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("gifts")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.GiftTiers.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.GiftTiers.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }

    private static GiftTier Map(GiftTierDto dto) => new()
    {
        Points = dto.Points,
        Title = dto.Title,
        TitleUz = dto.TitleUz,
        Description = dto.Description,
        DescriptionUz = dto.DescriptionUz,
        Image = dto.Image
    };
}
