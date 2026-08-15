namespace AuthApi.Services;

public record OdooProductDto(
    int OdooProductId,
    int OdooTemplateId,
    string Name,
    string? DefaultCode,
    string? Barcode,
    decimal Price,
    decimal Cost,
    string? CategoryName,
    string? Brand,
    bool InStock
);

public interface IOdooProductService
{
    /// <summary>
    /// Odoo'da is_published=true bo'lgan barcha mahsulot VARIANTLARINI (product.product)
    /// qaytaradi — brend va "Websayt" pricelist narxi bilan.
    /// </summary>
    Task<List<OdooProductDto>> GetPublishedProductsAsync();
}
