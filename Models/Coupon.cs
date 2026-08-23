namespace AuthApi.Models;

// Chegirma kuponi. Admin panelda yaratiladi, mijoz checkout paytida kodni
// kiritadi. UsedCount — barcha foydalanuvchilar bo'yicha umumiy ishlatilgan
// marta soni, UsageLimit'ga yetganda kupon avtomatik ishlamay qoladi.
public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // har doim katta harfda saqlanadi
    public string DiscountType { get; set; } = "percent"; // "percent" | "fixed"
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; } // faqat "percent" turi uchun yuqori chegara
    public decimal? MinOrderAmount { get; set; }
    public int? UsageLimit { get; set; } // umumiy ishlatish soni chegarasi, null = cheksiz
    public int UsedCount { get; set; }
    public int PerUserLimit { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Bitta muvaffaqiyatli qo'llanilgan kupon — qaysi foydalanuvchi, qaysi
// buyurtmada, qancha chegirma bilan ishlatgani. PerUserLimit'ni tekshirish
// uchun ham shu jadval ishlatiladi.
public class CouponRedemption
{
    public int Id { get; set; }
    public int CouponId { get; set; }
    public int UserId { get; set; }
    public int OrderId { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
