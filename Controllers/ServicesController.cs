using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Bosh sahifadagi "Xizmatlar" bloki.
[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Services.OrderBy(s => s.Order).ToListAsync());

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(ServiceDto dto)
    {
        var entity = new Service { Title = dto.Title, Description = dto.Description, Icon = dto.Icon, Order = dto.Order };
        _db.Services.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ServiceDto dto)
    {
        var entity = await _db.Services.FindAsync(id);
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
        var entity = await _db.Services.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.Services.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }
}
