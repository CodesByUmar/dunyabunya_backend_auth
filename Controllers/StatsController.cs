using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Bosh sahifadagi statistika (masalan "30 000+ Tovarlar").
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StatsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Stats.OrderBy(s => s.Order).ToListAsync());

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(StatDto dto)
    {
        var entity = new Stat { Value = dto.Value, Label = dto.Label, Order = dto.Order };
        _db.Stats.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, StatDto dto)
    {
        var entity = await _db.Stats.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        entity.Value = dto.Value;
        entity.Label = dto.Label;
        entity.Order = dto.Order;
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Stats.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.Stats.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }
}
