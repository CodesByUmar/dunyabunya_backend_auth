namespace AuthApi.Models;

public class GiftTier
{
    public int Id { get; set; }
    public int Points { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleUz { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionUz { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
}

// Har bir foydalanuvchining ball hisobi. Faqat serverning o'zi (buyurtma
// yaratilganda yoki sovg'a olinganda) o'zgartiradi — tashqi endpoint orqali
// to'g'ridan-to'g'ri o'zgartirib bo'lmaydi.
public class UserPoints
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Balance { get; set; }
    public int TotalEarned { get; set; }
    public int YearPoints { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class GiftCampaign
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameUz { get; set; } = string.Empty;
    public DateTime AnnouncementDate { get; set; }
    public DateTime SelectionStartDate { get; set; }
    public DateTime SelectionEndDate { get; set; }
    public DateTime DistributionDate { get; set; }
    public bool IsActive { get; set; }
}

public class UserGiftClaim
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CampaignId { get; set; }
    public int GiftTierId { get; set; }
    public int Quantity { get; set; }
    public int PointsSpent { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending"; // pending | approved | distributed
}
