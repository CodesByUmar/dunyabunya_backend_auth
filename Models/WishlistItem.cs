namespace AuthApi.Models;

// Mijozning sevimli mahsulotlari — foydalanuvchi qaysi qurilmadan kirmasin
// bir xil ro'yxatni ko'rishi uchun (avval faqat brauzerning o'zida saqlanardi).
public class WishlistItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AddWishlistItemDto
{
    public int ProductId { get; set; }
}
