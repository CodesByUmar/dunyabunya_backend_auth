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

    public AuthController(
        AppDbContext db,
        IConfiguration config,
        IPhoneNormalizerService phoneNormalizer,
        IOdooService odooService,
        IEmailDomainValidatorService emailDomainValidator)
    {
        _db = db;
        _config = config;
        _phoneNormalizer = phoneNormalizer;
        _odooService = odooService;
        _emailDomainValidator = emailDomainValidator;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        if (!await _emailDomainValidator.HasValidMailServerAsync(dto.Email))
        {
            return BadRequest(new { message = "Bu email manzili mavjud emas yoki xat qabul qila olmaydi." });
        }

        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
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
            Email = dto.Email,
            PhoneNumber = normalizedPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        // Odoo hali ulanmagan bo'lsa ham xavfsiz - NoOpOdooService null qaytaradi
        user.OdooPartnerId = await _odooService.GetOrCreatePartnerAsync(
            $"{dto.FirstName} {dto.LastName}", normalizedPhone, dto.Email);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = await GenerateAuthResponse(user);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Email yoki parol noto'g'ri." });
        }

        var response = await GenerateAuthResponse(user);
        return Ok(response);
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
            odooPartnerId = user.OdooPartnerId
        });
    }

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
            user.RefreshToken != dto.RefreshToken ||
            user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
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

    // --- Helper methods ---

    private async Task<AuthResponseDto> GenerateAuthResponse(User user)
    {
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
            double.Parse(_config["Jwt:RefreshTokenExpireDays"]!));

        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Email, user.Email)
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