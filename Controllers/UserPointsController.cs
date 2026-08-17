using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Foydalanuvchining ball hisobi — faqat O'QISH uchun. Yozish/o'zgartirish
// tashqaridan mumkin emas — ballar faqat OrdersController (buyurtma
// yaratilganda) va GiftClaimsController (sovg'a olinganda) orqali,
// server ichida avtomatik o'zgaradi.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserPointsController : ControllerBase
{
    private readonly AppDbContext _db;
    public UserPointsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMyPoints()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var points = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId.Value);
        return Ok(points ?? new UserPoints { UserId = userId.Value, Balance = 0, TotalEarned = 0, YearPoints = 0 });
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
