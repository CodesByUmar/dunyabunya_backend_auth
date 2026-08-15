using System.ComponentModel.DataAnnotations;
namespace AuthApi.Models;

// Admin panel xodimi (Admin/Superuser) yaratish uchun — "Foydalanuvchilar" ekrani.
public class CreateStaffDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(4)]
    public string Password { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    // "Admin" | "Superuser"
    [Required]
    public string Role { get; set; } = string.Empty;

    public List<string>? Permissions { get; set; }
}

// Tahrirlash — hamma maydon ixtiyoriy, faqat yuborilgani yangilanadi.
// Password bo'sh/yuborilmasa eski parol saqlanadi.
public class UpdateStaffDto
{
    [EmailAddress]
    public string? Email { get; set; }

    [MinLength(4)]
    public string? Password { get; set; }

    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; }
    public List<string>? Permissions { get; set; }
}
