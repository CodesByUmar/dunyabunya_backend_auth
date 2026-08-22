using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthApi.Data;
using AuthApi.Models;
using System.Security.Claims;
using Npgsql;

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

    // Bitta mijozning "Mijoz tafsilotlari" sahifasi uchun qo'shimcha,
    // hisoblab chiqariladigan statistika — asosiy ro'yxatni (GetUsers)
    // og'irlashtirmaslik uchun alohida, faqat kerak bo'lganda so'raladi.
    [HttpGet("users/{id:int}/stats")]
    public async Task<IActionResult> GetUserStats(int id)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == id);
        if (!userExists) return NotFound(new { message = "Foydalanuvchi topilmadi." });

        var orderStats = await _db.Orders
            .Where(o => o.UserId == id)
            .GroupBy(o => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Total), LastDate = g.Max(o => o.Date) })
            .FirstOrDefaultAsync();

        var points = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == id);
        var reviewCount = await _db.Reviews.CountAsync(r => r.UserId == id);
        var giftClaimCount = await _db.UserGiftClaims.CountAsync(c => c.UserId == id);

        return Ok(new
        {
            orderCount = orderStats?.Count ?? 0,
            totalSpent = orderStats?.Total ?? 0,
            lastOrderDate = orderStats?.LastDate,
            pointsBalance = points?.Balance ?? 0,
            totalPointsEarned = points?.TotalEarned ?? 0,
            reviewCount,
            giftClaimCount
        });
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

    // ==========================================
    // Admin panel xodimlari (Admin/Superuser) — "Foydalanuvchilar" ekrani.
    // Oddiy marketplace mijozlaridan (Role="Customer") ajratilgan holda ishlaydi.
    // ==========================================

    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff()
    {
        // Permissions'ni bazada emas, xotirada bo'lamiz (EF Core Split'ni SQL'ga
        // tarjima qila olmaydi) — shuning uchun avval to'liq entity tortiladi.
        var staff = await _db.Users
            .Where(u => u.Role == "Admin" || u.Role == "Superuser")
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(staff.Select(u => new
        {
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.PhoneNumber,
            u.Role,
            u.CreatedAt,
            Permissions = SplitPermissions(u.Permissions)
        }));
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff(CreateStaffDto dto)
    {
        var role = NormalizeRole(dto.Role);
        if (role == null)
        {
            return BadRequest(new { message = "Rol noto'g'ri. Faqat 'Admin' yoki 'Superuser' bo'lishi mumkin." });
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return BadRequest(new { message = "Bu email allaqachon ro'yxatdan o'tgan." });
        }

        var permissions = AdminSections.Sanitize(role, dto.Permissions);

        var user = new Models.User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            PhoneNumber = dto.PhoneNumber ?? "",
            FirstName = dto.FirstName ?? "",
            LastName = dto.LastName ?? "",
            Role = role,
            Permissions = permissions.Count > 0 ? string.Join(',', permissions) : null,
            AuthProvider = "local",
            EmailVerified = true
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return BadRequest(new { message = "Bu email yoki telefon raqam allaqachon ro'yxatdan o'tgan." });
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Role,
            user.CreatedAt,
            Permissions = permissions
        });
    }

    [HttpPatch("staff/{id:int}")]
    public async Task<IActionResult> UpdateStaff(int id, UpdateStaffDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null || (user.Role != "Admin" && user.Role != "Superuser"))
        {
            return NotFound(new { message = "Xodim topilmadi." });
        }

        var newRole = dto.Role != null ? NormalizeRole(dto.Role) : user.Role;
        if (newRole == null)
        {
            return BadRequest(new { message = "Rol noto'g'ri. Faqat 'Admin' yoki 'Superuser' bo'lishi mumkin." });
        }

        // XAVFSIZLIK: oxirgi Admin'ni Superuser'ga tushirib qo'yib, tizimda
        // hech kim to'liq huquqqa ega bo'lmay qolmasligi uchun.
        if (user.Role == "Admin" && newRole != "Admin" && await IsLastAdminAsync(user.Id))
        {
            return BadRequest(new { message = "Tizimdagi yagona Admin'ning rolini pasaytira olmaysiz." });
        }

        if (!string.IsNullOrEmpty(dto.Email))
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (normalizedEmail != user.Email && await _db.Users.AnyAsync(u => u.Id != id && u.Email == normalizedEmail))
            {
                return BadRequest(new { message = "Bu email allaqachon ro'yxatdan o'tgan." });
            }
            user.Email = normalizedEmail;
        }

        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
        if (dto.FirstName != null) user.FirstName = dto.FirstName;
        if (dto.LastName != null) user.LastName = dto.LastName;

        user.Role = newRole;
        var permissions = AdminSections.Sanitize(newRole, dto.Permissions ?? SplitPermissions(user.Permissions));
        user.Permissions = permissions.Count > 0 ? string.Join(',', permissions) : null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return BadRequest(new { message = "Bu email yoki telefon raqam allaqachon ro'yxatdan o'tgan." });
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Role,
            user.CreatedAt,
            Permissions = permissions
        });
    }

    [HttpDelete("staff/{id:int}")]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null || (user.Role != "Admin" && user.Role != "Superuser"))
        {
            return NotFound(new { message = "Xodim topilmadi." });
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(new { message = "O'zingizni o'chira olmaysiz." });
        }

        if (user.Role == "Admin" && await IsLastAdminAsync(user.Id))
        {
            return BadRequest(new { message = "Tizimdagi yagona Admin'ni o'chira olmaysiz." });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Xodim o'chirildi." });
    }

    // Frontend rolni kichik harfda yuborishi mumkin ("admin"/"superuser") — bazada
    // esa har doim "Admin"/"Superuser" (katta harf bilan) saqlanadi, chunki
    // [Authorize(Roles = "Admin")] va boshqa tekshiruvlar case-sensitive.
    private static string? NormalizeRole(string role) => role.Trim().ToLowerInvariant() switch
    {
        "admin" => "Admin",
        "superuser" => "Superuser",
        _ => null
    };

    private async Task<bool> IsLastAdminAsync(int excludingUserId) =>
        !await _db.Users.AnyAsync(u => u.Role == "Admin" && u.Id != excludingUserId);

    private static List<string> SplitPermissions(string? permissions) =>
        string.IsNullOrEmpty(permissions)
            ? new List<string>()
            : permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}