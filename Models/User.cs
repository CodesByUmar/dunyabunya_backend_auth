namespace AuthApi.Models;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string AuthProvider { get; set; } = "local";
    public string Role { get; set; } = "Customer";
    public int? OdooPartnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    // Email tasdiqlash: eski userlar uchun default TRUE (migration'da),
    // yangi ro'yxatdan o'tganlar uchun Register endpoint'da explicit FALSE qilinadi.
    public bool EmailVerified { get; set; } = true;
    public string? EmailVerificationCodeHash { get; set; }
    public DateTime? EmailVerificationCodeExpiry { get; set; }
}
