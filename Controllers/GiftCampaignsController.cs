using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Sovg'a kampaniyalari. O'qish ochiq, boshqarish Admin yoki "gifts"
// ruxsati bor Superuser.
[ApiController]
[Route("api/[controller]")]
public class GiftCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    public GiftCampaignsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.GiftCampaigns.AsQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        return Ok(await query.OrderByDescending(c => c.SelectionStartDate).ToListAsync());
    }

    [RequireSection("gifts")]
    [HttpPost]
    public async Task<IActionResult> Create(GiftCampaignDto dto)
    {
        var entity = Map(dto);
        _db.GiftCampaigns.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("gifts")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, GiftCampaignDto dto)
    {
        var entity = await _db.GiftCampaigns.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });

        entity.Name = dto.Name;
        entity.NameUz = dto.NameUz;
        entity.AnnouncementDate = dto.AnnouncementDate;
        entity.SelectionStartDate = dto.SelectionStartDate;
        entity.SelectionEndDate = dto.SelectionEndDate;
        entity.DistributionDate = dto.DistributionDate;
        entity.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("gifts")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.GiftCampaigns.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Topilmadi." });
        _db.GiftCampaigns.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }

    private static GiftCampaign Map(GiftCampaignDto dto) => new()
    {
        Name = dto.Name,
        NameUz = dto.NameUz,
        AnnouncementDate = dto.AnnouncementDate,
        SelectionStartDate = dto.SelectionStartDate,
        SelectionEndDate = dto.SelectionEndDate,
        DistributionDate = dto.DistributionDate,
        IsActive = dto.IsActive
    };
}
