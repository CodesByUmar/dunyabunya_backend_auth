using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Bosh sahifadagi hamkor/brend nomlari qatori.
[ApiController]
[Route("api/[controller]")]
public class PartnersController : ControllerBase
{
    private readonly AppDbContext _db;
    public PartnersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Partners.OrderBy(p => p.Order).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Partners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Hamkor topilmadi." });
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(PartnerDto dto)
    {
        var entity = new Partner { Name = dto.Name, Order = dto.Order };
        _db.Partners.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PartnerDto dto)
    {
        var entity = await _db.Partners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        entity.Name = dto.Name;
        entity.Order = dto.Order;
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Partners.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.Partners.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }
}
