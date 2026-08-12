namespace AuthApi.Services;

public record OdooProductDto(
    int OdooTemplateId,
    string Name,
    string? DefaultCode,
    string? Barcode,
    decimal Price,
    decimal Cost,
    string? CategoryName,
    string? Brand
);

public interface IOdooProductService
{
    /// <summary>Odoo'da is_published=true bo'lgan barcha mahsulotlarni (brend bilan) qaytaradi.</summary>
    Task<List<OdooProductDto>> GetPublishedProductsAsync();
}
