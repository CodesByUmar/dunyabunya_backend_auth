namespace AuthApi.Models;

public class CreateOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public List<CreateOrderItemDto> Items { get; set; } = new();

    public string? CouponCode { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;
    public string? DeliveryMethod { get; set; }
    public string? PickupBranchId { get; set; }
    public string? PickupBranchName { get; set; }
    public string? PickupDate { get; set; }
    public string? PickupTime { get; set; }
}

// Admin/Superuser tomonidan buyurtma holatini yangilash uchun.
public class UpdateOrderDto
{
    public string? Status { get; set; }
}
