using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthApi.Controllers;

// "Mening obyektlarim" — mijozning saqlangan yetkazib berish manzillari.
// Avval faqat brauzerda (localStorage) saqlanardi, endi qaysi qurilmadan
// kirmasin bir xil ro'yxat ko'rinadi.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PropertiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PropertiesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetProperties()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var properties = await _db.Properties
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Address,
                p.Lat,
                p.Lng,
                p.MapLink,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(properties);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProperty(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var property = await _db.Properties
            .Where(p => p.Id == id && p.UserId == userId)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Address,
                p.Lat,
                p.Lng,
                p.MapLink,
                p.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (property == null) return NotFound(new { message = "Obyekt topilmadi." });
        return Ok(property);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Address))
        {
            return BadRequest(new { message = "Obyekt nomi va manzil kiritilishi shart." });
        }

        var entity = new Property
        {
            UserId = userId.Value,
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            Lat = dto.Lat,
            Lng = dto.Lng,
            MapLink = dto.MapLink
        };

        _db.Properties.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            entity.Id,
            entity.Name,
            entity.Address,
            entity.Lat,
            entity.Lng,
            entity.MapLink,
            entity.CreatedAt
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProperty(int id, [FromBody] UpdatePropertyDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var entity = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (entity == null) return NotFound(new { message = "Obyekt topilmadi." });

        if (dto.Name != null) entity.Name = dto.Name.Trim();
        if (dto.Address != null) entity.Address = dto.Address.Trim();
        if (dto.Lat.HasValue) entity.Lat = dto.Lat.Value;
        if (dto.Lng.HasValue) entity.Lng = dto.Lng.Value;
        if (dto.MapLink != null) entity.MapLink = dto.MapLink;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            entity.Id,
            entity.Name,
            entity.Address,
            entity.Lat,
            entity.Lng,
            entity.MapLink,
            entity.CreatedAt
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProperty(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var entity = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (entity == null) return NotFound(new { message = "Obyekt topilmadi." });

        _db.Properties.Remove(entity);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Obyekt o'chirildi." });
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
