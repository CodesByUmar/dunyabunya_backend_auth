namespace AuthApi.Models;

// Mijozning saqlangan yetkazib berish manzillari ("Mening obyektlarim") —
// avval faqat brauzerda (localStorage) saqlanardi, shuning uchun boshqa
// qurilmadan kirganda ro'yxat yo'qolardi.
public class Property
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string MapLink { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreatePropertyDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string MapLink { get; set; } = string.Empty;
}

public class UpdatePropertyDto
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? MapLink { get; set; }
}
