using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPhoneNormalizerService _phoneNormalizer;
    private readonly IOdooService _odooService;
    private readonly IEmailDomainValidatorService _emailDomainValidator;
    private readonly IEmailService _emailService;

    public AuthController(
        AppDbContext db,
        IConfiguration config,
        IPhoneNormalizerService phoneNormalizer,
        IOdooService odooService,
        IEmailDomainValidatorService emailDomainValidator,
        IEmailService emailService)
    {
        _db = db;
        _config = config;
        _phoneNormalizer = phoneNormalizer;
        _odooService = odooService;
        _emailDomainValidator = emailDomainValidator;
        _emailService = emailService;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);

        if (!await _emailDomainValidator.HasValidMailServerAsync(normalizedEmail))
        {
            return BadRequest(new { message = "Bu email manzili mavjud emas yoki xat qabul qila olmaydi." });
        }

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return BadRequest(new { message = "Bu email allaqachon ro'yxatdan o'tgan." });
        }

        if (!_phoneNormalizer.TryNormalize(dto.PhoneNumber, out var normalizedPhone))
        {
            return BadRequest(new { message = "Telefon raqam formati noto'g'ri." });
        }

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone))
        {
            return BadRequest(new { message = "Bu telefon raqam allaqachon ro'yxatdan o'tgan." });
        }

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Customer",
            // Email hali tasdiqlanmagan — kimdir boshqa birovning email/telefonini
            // kiritib akkaunt ochib qo'yishining oldini olish uchun, login faqat
            // tasdiqlangandan keyin ruxsat etiladi (pastga qarang: Login, VerifyEmail).
            EmailVerified = false
        };

        user.OdooPartnerId = await _odooService.GetOrCreatePartnerAsync(
            $"{dto.FirstName} {dto.LastName}", normalizedPhone, normalizedEmail);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Tekshiruv va saqlash orasidagi parallel so'rovda duplicate paydo bo'lishi mumkin
            // (TOCTOU). DB darajasidagi UNIQUE index buni ushlaydi.
            return BadRequest(new { message = "Bu email yoki telefon raqam allaqachon ro'yxatdan o'tgan." });
        }

        await SendVerificationCodeAsync(user);

        return Ok(new
        {
            message = "Ro'yxatdan o'tish muvaffaqiyatli. Emailingizga yuborilgan kodni tasdiqlang.",
            email = user.Email,
            requiresVerification = true
        });
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginDto dto)
    {
        Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            };
            payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch (Google.Apis.Auth.InvalidJwtException)
        {
            return Unauthorized(new { message = "Google tokeni noto'g'ri yoki muddati tugagan." });
        }

        var normalizedEmail = NormalizeEmail(payload.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            user = new User
            {
                FirstName = payload.GivenName ?? "",
                LastName = payload.FamilyName ?? "",
                Email = normalizedEmail,
                PhoneNumber = "",
                PasswordHash = null,
                AuthProvider = "google",
                Role = "Customer",
                // Google token email egaligini tasdiqlagan bo'lsa-da, qo'shimcha xavfsizlik
                // qatlami sifatida baribir 6 xonali kod yuboriladi va tasdiqlangunicha
                // login berilmaydi (masalan: bir xil ismli boshqa odam sifatida ko'rsatilib
                // qolmasligi uchun).
                EmailVerified = false
            };

            user.OdooPartnerId = await _odooService.GetOrCreatePartnerAsync(
                $"{user.FirstName} {user.LastName}", user.PhoneNumber, normalizedEmail);

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Muvaffaqiyatsiz insert'ning entity'si hali 'Added' holatida track qilinadi —
                // keyingi SaveChanges uni yana insert qilmoqchi bo'ladi. Trackerni tozalaymiz,
                // keyin parallel so'rov yaratgan user'ni qayta yuklaymiz.
                _db.ChangeTracker.Clear();
                user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
                if (user == null)
                {
                    return Conflict(new { message = "Ro'yxatdan o'tishda xatolik yuz berdi." });
                }
            }
        }

        // Hali tasdiqlanmagan bo'lsa (yangi Google user yoki avval local ro'yxatdan
        // o'tib, kodni hech qachon tasdiqlamagan user) — kod yuboramiz va Login bilan
        // BIR XIL formatdagi 403 javobni qaytaramiz, frontend buni bir xil ishlaydi.
        if (!user.EmailVerified)
        {
            await SendVerificationCodeAsync(user);
            return StatusCode(403, new
            {
                message = "Email hali tasdiqlanmagan. Iltimos, avval emailingizga yuborilgan kodni tasdiqlang.",
                code = "EMAIL_NOT_VERIFIED",
                requiresVerification = true,
                email = user.Email
            });
        }

        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        User? user;

        if (dto.EmailOrPhone.Contains('@'))
        {
            var normalizedEmail = NormalizeEmail(dto.EmailOrPhone);
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        }
        else
        {
            if (!_phoneNormalizer.TryNormalize(dto.EmailOrPhone, out var normalizedPhone))
            {
                return Unauthorized(new { message = "Email/telefon yoki parol noto'g'ri." });
            }
            user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone);
        }

        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Email/telefon yoki parol noto'g'ri." });
        }

        if (!user.EmailVerified)
        {
            // XAVFSIZLIK/UX: 403 (Forbidden) + code="EMAIL_NOT_VERIFIED" — frontend
            // aynan shu status+code kombinatsiyasini kutadi (login-page.tsx), 401
            // "parol noto'g'ri" degan umumiy xato bilan aralashib ketmasligi uchun.
            return StatusCode(403, new
            {
                message = "Email hali tasdiqlanmagan. Iltimos, avval emailingizga yuborilgan kodni tasdiqlang.",
                code = "EMAIL_NOT_VERIFIED",
                requiresVerification = true,
                email = user.Email
            });
        }

        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponseDto>> VerifyEmail(VerifyEmailDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null ||
            user.EmailVerificationCodeExpiry == null ||
            user.EmailVerificationCodeExpiry < DateTime.UtcNow ||
            !SecureEquals(user.EmailVerificationCodeHash, HashToken(dto.Code)))
        {
            return BadRequest(new { message = "Kod noto'g'ri yoki muddati o'tgan." });
        }

        user.EmailVerified = true;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiry = null;
        await _db.SaveChangesAsync();

        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // XAVFSIZLIK: user enumeration'ning oldini olish uchun har doim bir xil javob.
        var genericResponse = new { message = "Agar bu email ro'yxatdan o'tgan va tasdiqlanmagan bo'lsa, yangi kod yuborildi." };

        // AuthProvider != "local" sharti OLIB TASHLANDI — endi Google orqali
        // ro'yxatdan o'tib, hali tasdiqlamagan userlar ham kodni qayta so'rashi mumkin.
        if (user == null || user.EmailVerified)
        {
            return Ok(genericResponse);
        }

        await SendVerificationCodeAsync(user);
        return Ok(genericResponse);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            phoneNumber = user.PhoneNumber,
            role = user.Role,
            odooPartnerId = user.OdooPartnerId
        });
    }

    // Google orqali birinchi marta kirgan (telefon/parolsiz) userlar profilni
    // to'ldirishi uchun, shuningdek istalgan user keyinroq telefon/parolini
    // yangilashi uchun ishlatiladi.
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPatch("complete-profile")]
    public async Task<IActionResult> CompleteProfile(CompleteProfileDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (dto.PhoneNumber == null && dto.Password == null)
        {
            return BadRequest(new { message = "Telefon raqam yoki parol kiritilishi shart." });
        }

        if (dto.PhoneNumber != null)
        {
            if (!_phoneNormalizer.TryNormalize(dto.PhoneNumber, out var normalizedPhone))
            {
                return BadRequest(new { message = "Telefon raqam formati noto'g'ri." });
            }

            if (await _db.Users.AnyAsync(u => u.Id != user.Id && u.PhoneNumber == normalizedPhone))
            {
                return BadRequest(new { message = "Bu telefon raqam allaqachon ro'yxatdan o'tgan." });
            }

            user.PhoneNumber = normalizedPhone;
        }

        if (dto.Password != null)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return BadRequest(new { message = "Bu telefon raqam allaqachon ro'yxatdan o'tgan." });
        }

        // Endi refresh token bekor qilinmaydi — foydalanuvchi qayta login qilishga
        // majburlanmasdan, shu yerning o'zida yangi token olib, avtomatik davom etadi.
        var response = await GenerateAuthResponse(user);

        return Ok(new
        {
            token = response.Token,
            refreshToken = response.RefreshToken,
            firstName = response.FirstName,
            lastName = response.LastName,
            email = response.Email,
            role = response.Role,
            phoneNumber = user.PhoneNumber,
            message = "Profil muvaffaqiyatli yangilandi."
        });
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto dto)
    {
        var principal = GetPrincipalFromExpiredToken(dto.Token);
        if (principal == null)
        {
            return Unauthorized(new { message = "Token noto'g'ri." });
        }

        var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Token noto'g'ri." });
        }

        var user = await _db.Users.FindAsync(userId);

        if (user == null ||
            !SecureEquals(user.RefreshToken, HashToken(dto.RefreshToken)) ||
            user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            // XAVFSIZLIK: token mos kelmasa (eski/o'g'irlangan refresh token qayta
            // ishlatilgan bo'lishi mumkin) — joriy saqlangan refresh tokenni ham
            // bekor qilamiz, shunda foydalanuvchi to'liq qayta login qilishga
            // majbur bo'ladi (session hijack'ning oldini olish).
            if (user != null && user.RefreshToken != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _db.SaveChangesAsync();
            }

            return Unauthorized(new { message = "Refresh token noto'g'ri yoki muddati tugagan. Qayta login qiling." });
        }

        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Chiqish muvaffaqiyatli amalga oshirildi." });
    }

    // ==========================================
    // Forgot / Reset password — 6 xonali kod orqali (link orqali EMAS)
    // ==========================================

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // XAVFSIZLIK: user topilmasa yoki Google orqali ro'yxatdan o'tgan (parolsiz)
        // bo'lsa ham, har doim bir xil generic xabar qaytariladi (user enumeration'ning
        // oldini olish uchun).
        var genericResponse = new { message = "Agar bu email ro'yxatdan o'tgan bo'lsa, tasdiqlash kodi yuborildi." };

        if (user == null || user.AuthProvider != "local")
        {
            return Ok(genericResponse);
        }

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6");

        // Kod DB'da ochiq emas, faqat SHA-256 hashi saqlanadi (mavjud
        // ResetPasswordToken/ResetPasswordTokenExpiry ustunlari qayta ishlatilgan —
        // endi "token" emas, "6 xonali kod hashi" saqlaydi).
        user.ResetPasswordToken = HashToken(code);
        user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        await _emailService.SendPasswordResetEmailAsync(user.Email, code);

        return Ok(genericResponse);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponseDto>> ResetPassword(ResetPasswordDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null ||
            user.ResetPasswordTokenExpiry == null ||
            user.ResetPasswordTokenExpiry < DateTime.UtcNow ||
            !SecureEquals(user.ResetPasswordToken, HashToken(dto.Code)))
        {
            return BadRequest(new { message = "Kod noto'g'ri yoki muddati o'tgan." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.ResetPasswordToken = null;
        user.ResetPasswordTokenExpiry = null;

        // Qaytadan login qilish shart emas — GenerateAuthResponse eski refresh
        // tokenni yangisi bilan almashtiradi, shuning uchun o'g'irlangan bo'lishi
        // mumkin bo'lgan eski sessiya baribir avtomatik bekor bo'ladi.
        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    // --- Helper methods ---

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<AuthResponseDto> GenerateAuthResponse(User user)
    {
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Refresh token DB'da ochiq emas, faqat SHA-256 hashi saqlanadi.
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
            double.Parse(_config["Jwt:RefreshTokenExpireDays"]!));

        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role
        };
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expireMinutes = double.Parse(_config["Jwt:AccessTokenExpireMinutes"]!);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// DB'da saqlash uchun token'ni SHA-256 bilan hashlaydi. Tokenlar 64 random
    /// baytdan iborat (yuqori entropiya), shuning uchun salt kerak emas.
    /// </summary>
    private static string? HashToken(string? token)
    {
        // JSON'da null kelishi mumkin ([Required] yorlig'i yo'q) — 500 o'rniga
        // null qaytaramiz, taqqoslash xatosiz 401/400 beradi.
        if (string.IsNullOrEmpty(token)) return null;

        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// DbUpdateException PostgreSQL unique_violation (SQLSTATE 23505) tufayli
    /// yuzaga kelganmi?
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg && pg.SqlState == "23505";
    }

    /// <summary>
    /// 6 xonali tasdiqlash kodi yaratadi, hashini DB'ga saqlaydi (15 daqiqa amal
    /// qiladi) va emailga yuboradi. AuthPolicy rate-limiter (15 daqiqada 20 so'rov)
    /// kod muddati ichida uni brute-force qilishni amalda imkonsiz qiladi
    /// (1 000 000 variant / <=20 urinish).
    /// </summary>
    private async Task SendVerificationCodeAsync(User user)
    {
        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6");

        user.EmailVerificationCodeHash = HashToken(code);
        user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        await _emailService.SendVerificationCodeEmailAsync(user.Email, code);
    }

    /// <summary>
    /// Constant-time string solishtirish — hash/tokenlarni taqqoslashda timing
    /// side-channel xavfini kamaytiradi.
    /// </summary>
    private static bool SecureEquals(string? a, string? b)
    {
        if (a == null || b == null) return a == b;

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        if (aBytes.Length != bBytes.Length) return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)),
            ValidIssuer = _config["Jwt:Issuer"],
            ValidAudience = _config["Jwt:Audience"],
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}