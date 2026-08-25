using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Bosh sahifadagi "Afzalliklar" bloki.
[ApiController]
[Route("api/[controller]")]
public class AdvantagesController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdvantagesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Advantages.OrderBy(a => a.Order).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Advantages.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Afzallik topilmadi." });
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(AdvantageDto dto)
    {
        var entity = new Advantage { Title = dto.Title, Description = dto.Description, Icon = dto.Icon, Order = dto.Order };
        _db.Advantages.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AdvantageDto dto)
    {
        var entity = await _db.Advantages.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Icon = dto.Icon;
        entity.Order = dto.Order;
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Advantages.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.Advantages.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }
}
