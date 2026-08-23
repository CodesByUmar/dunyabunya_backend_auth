using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Umumiy xizmatga (yetkazib berish, servis) baho — buyurtmadan keyin so'raladi.
[ApiController]
[Route("api/[controller]")]
public class ServiceReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServiceReviewsController(AppDbContext db) => _db = db;

    [RequireSection("reviews")]
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.ServiceReviews.OrderByDescending(r => r.Date).ToListAsync());

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceReviewDto dto)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        if (dto.Rating < 1 || dto.Rating > 5)
        {
            return BadRequest(new { message = "Baho 1 dan 5 gacha bo'lishi kerak." });
        }

        var review = new ServiceReview
        {
            UserId = user.Id,
            UserName = $"{user.FirstName} {user.LastName}".Trim(),
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        _db.ServiceReviews.Add(review);
        await _db.SaveChangesAsync();

        return Ok(review);
    }

    // Sharh egasi o'zi o'chira oladi (moderatsiya ruxsati shart emas);
    // Admin/"reviews" ruxsatli Superuser esa istalgan sharhni o'chira oladi.
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var review = await _db.ServiceReviews.FindAsync(id);
        if (review == null) return NotFound(new { message = "Sharh topilmadi." });

        if (review.UserId != userId && !CanModerateReviews())
        {
            return Forbid();
        }

        _db.ServiceReviews.Remove(review);
        await _db.SaveChangesAsync();

        return Ok(new { message = "O'chirildi." });
    }

    private bool CanModerateReviews()
    {
        if (User.IsInRole("Admin")) return true;
        if (!User.IsInRole("Superuser")) return false;

        var permissions = User.FindFirstValue("permissions")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        return permissions.Contains("reviews");
    }
}
