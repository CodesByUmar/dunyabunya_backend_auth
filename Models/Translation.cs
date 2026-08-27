namespace AuthApi.Models;

// Frontend UI matnlari (ru/uz) — avval to'g'ridan-to'g'ri frontend kodida
// (translations.ts) yozilgan edi, endi backendda saqlanadi va admin panel
// orqali qayta deploy qilmasdan tahrirlash mumkin.
public class Translation
{
    public int Id { get; set; }
    public string App { get; set; } = string.Empty; // "user" | "admin"
    public string Key { get; set; } = string.Empty; // nuqta bilan ajratilgan yo'l, masalan "chat.greeting"
    public string Ru { get; set; } = string.Empty;
    public string Uz { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
