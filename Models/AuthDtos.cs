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
    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Yangi DTO — refresh endpointi uchun
public class RefreshRequestDto
{
    public string Token { get; set; } = string.Empty;        // eski (muddati o'tgan bo'lishi mumkin) access token
    public string RefreshToken { get; set; } = string.Empty;
}