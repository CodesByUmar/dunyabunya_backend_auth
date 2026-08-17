using System.Security.Claims;
using AuthApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Foydalanuvchi bildirishnomalari — har doim JOriy foydalanuvchining O'ZINIKI,
// boshqa birovning bildirishnomasini ko'rish/o'zgartirish imkonsiz (userId
// so'rovdan emas, JWT'dan olinadi).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var notification = await _db.Notifications.FindAsync(id);
        if (notification == null || notification.UserId != userId.Value)
        {
            return NotFound(new { message = "Bildirishnoma topilmadi." });
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(notification);
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
