namespace AuthApi.Models;

public class UpdateProductDescriptionDto
{
    public string? Description { get; set; }
}

// Faqat Name/CategoryName — Narx (Price) admin panelda umuman tahrirlanmaydi,
// doim Odoo'dan sinxronlanadi. Ikkalasi ham ixtiyoriy: faqat kiritilgani
// yangilanadi (masalan faqat nomini o'zgartirmoqchi bo'lsa, kategoriyaga tegilmaydi).
public class UpdateProductDetailsDto
{
    public string? Name { get; set; }
    public string? CategoryName { get; set; }
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
