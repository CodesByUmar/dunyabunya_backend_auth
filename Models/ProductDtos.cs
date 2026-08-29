namespace AuthApi.Models;

public class UpdateProductDescriptionDto
{
    public string? Description { get; set; }
}

// Faqat Name — Narx (Price) va Kategoriya (CategoryName) admin panelda
// tahrirlanmaydi (Kategoriya frontendning original Odoo yo'liga qattiq
// bog'langan mantig'i tuzatilmaguncha xavfli — "Kategoriya tahriri" ADR'ga q.).
public class UpdateProductDetailsDto
{
    public string? Name { get; set; }
}

public class ProductSpecificationDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ProductApprovalDto
{
    public string Status { get; set; } = string.Empty;
}
