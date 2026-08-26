namespace AuthApi.Models;

public class UpdateProductDescriptionDto
{
    public string? Description { get; set; }
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
