namespace AuthApi.Services;

/// <summary>
/// Vaqtinchalik implementatsiya - Odoo hali ulanmagan.
/// Odoo tayyor bo'lganda bu klass o'rniga haqiqiy OdooService yoziladi,
/// Program.cs'da faqat bitta DI qatori almashtiriladi.
/// </summary>
public class NoOpOdooService : IOdooService
{
    public Task<int?> GetOrCreatePartnerAsync(string fullName, string phone, string email)
        => Task.FromResult<int?>(null);
}