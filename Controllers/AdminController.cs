using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthApi.Data;
using System.Security.Claims;

namespace AuthApi.Controllers;

// Bu controller'dagi HAR BIR endpoint faqat Role = "Admin" bo'lgan
// foydalanuvchilar uchun ochiq. Boshqa rol (masalan "Customer") bilan
// kirishga urinsa, 403 Forbidden qaytadi.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // Admin panel uchun: barcha foydalanuvchilar ro'yxati.
    // Frontend'da bu GET /api/Admin/users orqali chaqiriladi,
    // JWT token Authorization header'da (Bearer ...) yuborilishi shart.
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PhoneNumber,
                u.Role,
                u.AuthProvider,
                u.CreatedAt
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    // Admin boshqa foydalanuvchining rolini o'zgartirishi uchun (masalan
    // birovni Admin qilib tayinlash).
    [HttpPatch("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Foydalanuvchi topilmadi." });

        var allowedRoles = new[] { "Customer", "Admin" };
        if (!allowedRoles.Contains(dto.Role))
        {
            return BadRequest(new { message = "Rol noto'g'ri. Faqat 'Customer' yoki 'Admin' bo'lishi mumkin." });
        }

        // XAVFSIZLIK: admin o'zini-o'zi pasaytirib, tizimdan qulflanib qolmasligi uchun.
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(currentUserIdStr, out var currentUserId) &&
            currentUserId == id && dto.Role != "Admin")
        {
            return BadRequest(new { message = "O'zingizning Admin rolingizni pasaytira olmaysiz." });
        }

        user.Role = dto.Role;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rol muvaffaqiyatli yangilandi.", user.Id, user.Role });
    }
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}