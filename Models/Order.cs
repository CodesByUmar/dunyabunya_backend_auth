namespace AuthApi.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }

    // Buyurtma paytidagi mijoz ma'lumotlari (denormalizatsiya — keyinroq
    // foydalanuvchi profilini o'zgartirsa ham, buyurtma tarixi o'zgarmaydi).
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public decimal Total { get; set; }
    public string Status { get; set; } = "pending"; // pending|processing|shipped|delivered|cancelled|received
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string PaymentMethod { get; set; } = string.Empty;

    public string? DeliveryMethod { get; set; } // "delivery" | "pickup"
    public string? PickupBranchId { get; set; }
    public string? PickupBranchName { get; set; }
    public string? PickupDate { get; set; }
    public string? PickupTime { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty; // buyurtma paytidagi nom
    public int Quantity { get; set; }
    public decimal Price { get; set; } // buyurtma paytidagi narx (keyingi narx o'zgarishiga ta'sirlanmaydi)
}
