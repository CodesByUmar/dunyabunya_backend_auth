namespace AuthApi.Models;

public class CreateTranslationDto
{
    public string App { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Ru { get; set; } = string.Empty;
    public string Uz { get; set; } = string.Empty;
}

// Key/App qasddan kiritilmagan — mavjud tarjimani "ko'chirib" qo'yish emas,
// faqat matnini (ru/uz) tahrirlash mo'ljallangan (frontend kodi shu Key'ga
// qattiq bog'langan bo'ladi).
public class UpdateTranslationDto
{
    public string? Ru { get; set; }
    public string? Uz { get; set; }
}
