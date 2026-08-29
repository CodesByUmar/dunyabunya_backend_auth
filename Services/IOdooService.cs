namespace AuthApi.Services;

public interface IOdooService
{
    /// <summary>
    /// Odoo'da mavjud mijozni topadi yoki yangisini yaratadi.
    /// Hozircha (Odoo ulanmaguncha) NoOpOdooService orqali doim null qaytaradi.
    /// </summary>
    Task<int?> GetOrCreatePartnerAsync(string fullName, string phone, string email);

    /// <summary>
    /// Marketplace'da tushgan buyurtmani Odoo'da sale.order (qoralama/quotation
    /// holatida, tasdiqlanmagan) sifatida yaratadi. partnerId — mijozning Odoo
    /// res.partner ID'si; lines — (OdooProductId, Quantity, PriceUnit) ro'yxati.
    /// Yaratilgan sale.order ID'sini qaytaradi.
    /// </summary>
    Task<int> CreateSaleOrderAsync(int partnerId, string clientOrderRef, IReadOnlyList<(int OdooProductId, int Quantity, decimal PriceUnit)> lines);
}