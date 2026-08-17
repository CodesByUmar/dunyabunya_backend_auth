namespace AuthApi.Models;

// Mahsulot sharhi.
public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty; // yozilgan paytdagi nom (denormalizatsiya)
    public string Author { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? City { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Pros { get; set; }
    public string? Cons { get; set; }
    public string[] Photos { get; set; } = Array.Empty<string>();
    public bool Recommends { get; set; } = true;
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Reply { get; set; }
}

// Sotuvdan keyingi xizmat baholash (buyurtmaga emas, umumiy xizmatga).
public class ServiceReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

// "Aloqa" sahifasidan yuborilgan xabar.
public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

// Foydalanuvchiga bildirishnoma (masalan admin sharhga javob yozganda).
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = "review_reply";
    public int ProductId { get; set; }
    public int ReviewId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ReplyText { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
