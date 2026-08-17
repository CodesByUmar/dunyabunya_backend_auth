using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

// Buyurtmalar. Har doim [Authorize] — mehmon buyurtma bera olmaydi.
// Oddiy mijoz faqat O'Z buyurtmalarini ko'radi; Admin/Superuser+"orders"
// barchasini ko'radi va holatni o'zgartira oladi.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private static readonly string[] ValidStatuses =
        { "pending", "processing", "shipped", "delivered", "cancelled", "received" };

    private readonly AppDbContext _db;
    public OrdersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = CanManageOrders() ? _db.Orders.AsQueryable() : _db.Orders.Where(o => o.UserId == userId.Value);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.Date)
            .Select(o => Project(o))
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Buyurtma topilmadi." });

        if (order.UserId != userId.Value && !CanManageOrders())
        {
            return Forbid();
        }

        return Ok(Project(order));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (dto.Items.Count == 0)
        {
            return BadRequest(new { message = "Buyurtmada kamida bitta mahsulot bo'lishi kerak." });
        }

        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        var productsById = products.ToDictionary(p => p.Id);

        var items = new List<OrderItem>();
        decimal total = 0;

        foreach (var itemDto in dto.Items)
        {
            if (!productsById.TryGetValue(itemDto.ProductId, out var product))
            {
                return BadRequest(new { message = $"Mahsulot topilmadi (ID: {itemDto.ProductId})." });
            }

            if (itemDto.Quantity < 1)
            {
                return BadRequest(new { message = "Mahsulot soni kamida 1 bo'lishi kerak." });
            }

            // XAVFSIZLIK: narx mijozdan emas, bazadagi haqiqiy narxdan olinadi —
            // aks holda kimdir narxni o'zgartirib yuborishi mumkin edi.
            items.Add(new OrderItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Quantity = itemDto.Quantity,
                Price = product.Price
            });
            total += product.Price * itemDto.Quantity;
        }

        var order = new Order
        {
            UserId = userId.Value,
            CustomerName = dto.Name,
            CustomerPhone = dto.Phone,
            CustomerAddress = dto.Address,
            CustomerEmail = dto.Email,
            Lat = dto.Lat,
            Lng = dto.Lng,
            Total = total,
            Status = "pending",
            PaymentMethod = dto.PaymentMethod,
            DeliveryMethod = dto.DeliveryMethod,
            PickupBranchId = dto.PickupBranchId,
            PickupBranchName = dto.PickupBranchName,
            PickupDate = dto.PickupDate,
            PickupTime = dto.PickupTime,
            Items = items
        };

        _db.Orders.Add(order);

        // Har 100 000 so'mga 1 ball — server ichida avtomatik qo'shiladi,
        // mijoz ballarni to'g'ridan-to'g'ri o'zgartira olmaydi.
        var pointsToAdd = (int)Math.Floor(total / 100_000m);
        if (pointsToAdd > 0)
        {
            var points = await _db.UserPoints.FirstOrDefaultAsync(p => p.UserId == userId.Value);
            if (points == null)
            {
                points = new UserPoints { UserId = userId.Value };
                _db.UserPoints.Add(points);
            }
            points.Balance += pointsToAdd;
            points.TotalEarned += pointsToAdd;
            points.YearPoints += pointsToAdd;
            points.LastUpdated = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(Project(order));
    }

    [RequireSection("orders")]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateOrder(int id, UpdateOrderDto dto)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Buyurtma topilmadi." });

        if (dto.Status != null)
        {
            if (!ValidStatuses.Contains(dto.Status))
            {
                return BadRequest(new { message = "Holat noto'g'ri." });
            }
            order.Status = dto.Status;
        }

        await _db.SaveChangesAsync();
        return Ok(Project(order));
    }

    private bool CanManageOrders()
    {
        if (User.IsInRole("Admin")) return true;
        if (!User.IsInRole("Superuser")) return false;

        var permissions = User.FindFirstValue("permissions")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        return permissions.Contains("orders");
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : null;
    }

    private static object Project(Order o) => new
    {
        id = o.Id,
        customer = new
        {
            userId = o.UserId,
            name = o.CustomerName,
            phone = o.CustomerPhone,
            address = o.CustomerAddress,
            email = o.CustomerEmail,
            lat = o.Lat,
            lng = o.Lng
        },
        items = o.Items.Select(i => new { productId = i.ProductId, name = i.Name, quantity = i.Quantity, price = i.Price }),
        total = o.Total,
        status = o.Status,
        date = o.Date,
        paymentMethod = o.PaymentMethod,
        deliveryMethod = o.DeliveryMethod,
        pickupBranchId = o.PickupBranchId,
        pickupBranchName = o.PickupBranchName,
        pickupDate = o.PickupDate,
        pickupTime = o.PickupTime
    };
}
