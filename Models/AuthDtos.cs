using System.ComponentModel.DataAnnotations;
namespace AuthApi.Models;

public class RegisterDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required, MinLength(8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "Parol kamida bitta harf va bitta raqamdan iborat bo'lishi kerak.")]
    public string Password { get; set; } = string.Empty;
}
public class LoginDto
{
    [Required]
    public string EmailOrPhone { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
}
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class GoogleLoginDto
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

public class RefreshRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

// Endi link/token orqali EMAS — ro'yxatdan o'tishdagi bilan bir xil uslubda,
// emailga 6 xonali kod yuborish orqali ishlaydi.
public class ResetPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "Parol kamida bitta harf va bitta raqamdan iborat bo'lishi kerak.")]
    public string NewPassword { get; set; } = string.Empty;
}

public class VerifyEmailDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}

public class ResendVerificationDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class CompleteProfileDto
{
    // Ikkalasi ham ixtiyoriy — faqat yuborilgan maydon yangilanadi
    // (masalan faqat parolni o'zgartirish uchun faqat Password yuboriladi).
    public string? PhoneNumber { get; set; }

    [MinLength(8), RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "Parol kamida bitta harf va bitta raqamdan iborat bo'lishi kerak.")]
    public string? Password { get; set; }
}
