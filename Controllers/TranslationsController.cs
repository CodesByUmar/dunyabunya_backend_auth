using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AuthApi.Controllers;

// Frontend UI matnlari (ru/uz) — avval frontend kodida (translations.ts) qattiq
// yozilgan edi, endi shu yerda saqlanadi. O'qish — ochiq (frontend shundan
// o'qib ishlatadi), yozish — Admin/"translations" ruxsatli Superuser.
[ApiController]
[Route("api/[controller]")]
public class TranslationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TranslationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? app)
    {
        var query = _db.Translations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(app)) query = query.Where(t => t.App == app);

        var items = await query
            .OrderBy(t => t.App).ThenBy(t => t.Key)
            .Select(t => new { t.Id, t.App, t.Key, t.Ru, t.Uz, t.UpdatedAt })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Translations.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Tarjima topilmadi." });
        return Ok(entity);
    }

    [RequireSection("translations")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateTranslationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.App) || string.IsNullOrWhiteSpace(dto.Key))
        {
            return BadRequest(new { message = "app va key kiritilishi shart." });
        }

        var entity = new Translation
        {
            App = dto.App.Trim(),
            Key = dto.Key.Trim(),
            Ru = dto.Ru,
            Uz = dto.Uz
        };

        _db.Translations.Add(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return BadRequest(new { message = "Bu app+key juftligi allaqachon mavjud." });
        }

        return Ok(entity);
    }

    [RequireSection("translations")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTranslationDto dto)
    {
        var entity = await _db.Translations.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Tarjima topilmadi." });

        if (dto.Ru != null) entity.Ru = dto.Ru;
        if (dto.Uz != null) entity.Uz = dto.Uz;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("translations")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Translations.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Tarjima topilmadi." });

        _db.Translations.Remove(entity);
        await _db.SaveChangesAsync();

        return Ok(new { message = "O'chirildi." });
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}
