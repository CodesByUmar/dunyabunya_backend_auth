using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Sovg'a olish (ball evaziga). Ball yechish har doim shu yerda, server
// ichida, bitta atomik amal sifatida bajariladi — mijoz "yangi balansni"
// yubormaydi, faqat qaysi sovg'ani xohlayotganini aytadi.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GiftClaimsController : ControllerBase
{
    private readonly AppDbContext _db;
    public GiftClaimsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetClaims()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = CanManageGifts() ? _db.UserGiftClaims.AsQueryable() : _db.UserGiftClaims.Where(c => c.UserId == userId.Value);
        return Ok(await query.OrderByDescending(c => c.ClaimedAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> ClaimGift(CreateGiftClaimDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (dto.Quantity < 1)
        {
            return BadRequest(new { message = "Miqdor kamida 1 bo'lishi kerak." });
        }

        var campaign = await _db.GiftCampaigns.FindAsync(dto.CampaignId);
        if (campaign == null || !campaign.IsActive)
        {
            return BadRequest(new { message = "Kampaniya faol emas." });
        }

        var now = DateTime.UtcNow;
        if (now < campaign.SelectionStartDate || now > campaign.SelectionEndDate)
        {
            return BadRequest(new { message = "Sovg'a tanlash muddati tugagan yoki hali boshlanmagan." });
        }

        var tier = await _db.GiftTiers.FindAsync(dto.GiftTierId);
        if (tier == null) return NotFound(new { message = "Sovg'a topilmadi." });

        var totalPoints = tier.Points * dto.Quantity;

        var points = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (points == null || points.Balance < totalPoints)
        {
            return BadRequest(new { message = "Ballaringiz yetarli emas." });
        }

        points.Balance -= totalPoints;
        points.LastUpdated = DateTime.UtcNow;

        var claim = new UserGiftClaim
        {
            UserId = userId.Value,
            CampaignId = dto.CampaignId,
            GiftTierId = dto.GiftTierId,
            Quantity = dto.Quantity,
            PointsSpent = totalPoints,
            Status = "pending"
        };

        _db.UserGiftClaims.Add(claim);
        await _db.SaveChangesAsync();

        return Ok(claim);
    }

    [RequireSection("gifts")]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateClaim(int id, UpdateGiftClaimDto dto)
    {
        var claim = await _db.UserGiftClaims.FindAsync(id);
        if (claim == null) return NotFound(new { message = "Topilmadi." });

        var validStatuses = new[] { "pending", "approved", "distributed" };
        if (!validStatuses.Contains(dto.Status))
        {
            return BadRequest(new { message = "Holat noto'g'ri." });
        }

        claim.Status = dto.Status;
        await _db.SaveChangesAsync();
        return Ok(claim);
    }

    private bool CanManageGifts()
    {
        if (User.IsInRole("Admin")) return true;
        if (!User.IsInRole("Superuser")) return false;

        var permissions = User.FindFirstValue("permissions")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        return permissions.Contains("gifts");
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
