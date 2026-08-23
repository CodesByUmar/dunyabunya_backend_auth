using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

// Kupon tekshirish/hisoblash mantig'i — checkout'dagi oldindan tekshirish
// (CouponsController.Validate) va haqiqiy buyurtma yaratish
// (OrdersController.CreateOrder) ikkalasida ham bir xil qoidalar ishlatilishi
// uchun bitta joyda saqlanadi.
public static class CouponService
{
    public static async Task<(Coupon? coupon, string error)> FindValidCouponAsync(
        AppDbContext db, string code, decimal orderTotal, int userId)
    {
        var normalized = NormalizeCode(code);
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized);

        if (coupon == null || !coupon.IsActive)
        {
            return (null, "Kupon kodi noto'g'ri yoki faol emas.");
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
        {
            return (null, "Kupon muddati tugagan.");
        }

        if (coupon.MinOrderAmount.HasValue && orderTotal < coupon.MinOrderAmount.Value)
        {
            return (null, $"Kupondan foydalanish uchun buyurtma summasi kamida {coupon.MinOrderAmount.Value:0} so'm bo'lishi kerak.");
        }

        if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
        {
            return (null, "Kupon limiti tugagan.");
        }

        var userUsageCount = await db.CouponRedemptions.CountAsync(r => r.CouponId == coupon.Id && r.UserId == userId);
        if (userUsageCount >= coupon.PerUserLimit)
        {
            return (null, "Siz bu kupondan allaqachon foydalangansiz.");
        }

        return (coupon, string.Empty);
    }

    public static decimal CalculateDiscount(Coupon coupon, decimal orderTotal)
    {
        var discount = coupon.DiscountType == "percent"
            ? Math.Round(orderTotal * coupon.DiscountValue / 100m, 2)
            : coupon.DiscountValue;

        if (coupon.MaxDiscountAmount.HasValue)
        {
            discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);
        }

        // Chegirma buyurtma summasidan oshib, umumiy summani manfiy qilib
        // yubormasligi kerak.
        return Math.Min(discount, orderTotal);
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
