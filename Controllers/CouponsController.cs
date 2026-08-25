using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthApi.Controllers;

// Chegirma kuponlari. Admin/"coupons" ruxsatli Superuser boshqaradi, mijoz
// checkout paytida kodni tekshiradi (Validate) — haqiqiy sarflash esa faqat
// OrdersController.CreateOrder'da, buyurtma yaratilganda sodir bo'ladi.
[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly AppDbContext _db;
    public CouponsController(AppDbContext db) => _db = db;

    [RequireSection("coupons")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var coupons = await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return Ok(coupons);
    }

    [RequireSection("coupons")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Coupons.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Kupon topilmadi." });
        return Ok(entity);
    }

    [RequireSection("coupons")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCouponDto dto)
    {
        var code = CouponService.NormalizeCode(dto.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { message = "Kupon kodi kiritilishi shart." });
        }

        if (!IsValidDiscountType(dto.DiscountType))
        {
            return BadRequest(new { message = "Chegirma turi \"percent\" yoki \"fixed\" bo'lishi kerak." });
        }

        if (dto.DiscountValue <= 0)
        {
            return BadRequest(new { message = "Chegirma qiymati 0 dan katta bo'lishi kerak." });
        }

        if (dto.DiscountType == "percent" && dto.DiscountValue > 100)
        {
            return BadRequest(new { message = "Foizli chegirma 100 dan oshmasligi kerak." });
        }

        var entity = new Coupon
        {
            Code = code,
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            MaxDiscountAmount = dto.MaxDiscountAmount,
            MinOrderAmount = dto.MinOrderAmount,
            UsageLimit = dto.UsageLimit,
            PerUserLimit = Math.Max(dto.PerUserLimit, 1),
            IsActive = dto.IsActive,
            ExpiresAt = dto.ExpiresAt
        };

        _db.Coupons.Add(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return BadRequest(new { message = "Bu kupon kodi allaqachon mavjud." });
        }

        return Ok(entity);
    }

    [RequireSection("coupons")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCouponDto dto)
    {
        var entity = await _db.Coupons.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Kupon topilmadi." });

        if (dto.DiscountType != null)
        {
            if (!IsValidDiscountType(dto.DiscountType))
            {
                return BadRequest(new { message = "Chegirma turi \"percent\" yoki \"fixed\" bo'lishi kerak." });
            }
            entity.DiscountType = dto.DiscountType;
        }

        if (dto.DiscountValue.HasValue)
        {
            if (dto.DiscountValue.Value <= 0)
            {
                return BadRequest(new { message = "Chegirma qiymati 0 dan katta bo'lishi kerak." });
            }
            entity.DiscountValue = dto.DiscountValue.Value;
        }

        if (entity.DiscountType == "percent" && entity.DiscountValue > 100)
        {
            return BadRequest(new { message = "Foizli chegirma 100 dan oshmasligi kerak." });
        }

        if (dto.MaxDiscountAmount.HasValue) entity.MaxDiscountAmount = dto.MaxDiscountAmount;
        if (dto.MinOrderAmount.HasValue) entity.MinOrderAmount = dto.MinOrderAmount;
        if (dto.UsageLimit.HasValue) entity.UsageLimit = dto.UsageLimit;
        if (dto.PerUserLimit.HasValue) entity.PerUserLimit = Math.Max(dto.PerUserLimit.Value, 1);
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
        if (dto.ExpiresAt.HasValue) entity.ExpiresAt = dto.ExpiresAt;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [RequireSection("coupons")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Coupons.FindAsync(id);
        if (entity == null) return NotFound(new { message = "Kupon topilmadi." });

        _db.Coupons.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { message = "O'chirildi." });
    }

    // Checkout sahifasida kod kiritilganda, buyurtma yaratilishidan OLDIN
    // chegirma qanchaligini ko'rsatish uchun. Bu yerda kupon HALI sarflanmaydi.
    [Authorize]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(ValidateCouponDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (coupon, error) = await CouponService.FindValidCouponAsync(_db, dto.Code, dto.OrderTotal, userId.Value);
        if (coupon == null) return BadRequest(new { message = error });

        var discount = CouponService.CalculateDiscount(coupon, dto.OrderTotal);
        return Ok(new
        {
            valid = true,
            discountAmount = discount,
            discountType = coupon.DiscountType,
            discountValue = coupon.DiscountValue
        });
    }

    private static bool IsValidDiscountType(string type) => type == "percent" || type == "fixed";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
