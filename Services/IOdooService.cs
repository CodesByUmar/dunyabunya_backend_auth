namespace AuthApi.Services;

public interface IOdooService
{
    /// <summary>
    /// Odoo'da mavjud mijozni topadi yoki yangisini yaratadi.
    /// Hozircha (Odoo ulanmaguncha) NoOpOdooService orqali doim null qaytaradi.
    /// </summary>
    Task<int?> GetOrCreatePartnerAsync(string fullName, string phone, string email);
}