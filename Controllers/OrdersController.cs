using System.Security.Claims;
using AuthApi.Data;
using AuthApi.Filters;
using AuthApi.Models;
using AuthApi.Services;
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

        decimal discountAmount = 0;
        Coupon? coupon = null;
        string? appliedCouponCode = null;

        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
        {
            var (foundCoupon, error) = await CouponService.FindValidCouponAsync(_db, dto.CouponCode, total, userId.Value);
            if (foundCoupon == null)
            {
                return BadRequest(new { message = error });
            }

            coupon = foundCoupon;
            appliedCouponCode = foundCoupon.Code;
            discountAmount = CouponService.CalculateDiscount(foundCoupon, total);

            // Kuponni atomik ravishda "sarflaymiz" — bir vaqtda ko'p so'rov kelsa
            // ham umumiy UsageLimit'dan oshib ketmasligi uchun shartli UPDATE va
            // ta'sirlangan qatorlar sonini tekshirish orqali (mahsulot/ball
            // yangilanishlarida ishlatilgan xuddi shu naqsh).
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Coupons"" SET ""UsedCount"" = ""UsedCount"" + 1
                WHERE ""Id"" = {coupon.Id} AND ""IsActive"" = true
                  AND (""UsageLimit"" IS NULL OR ""UsedCount"" < ""UsageLimit"")");

            if (affected == 0)
            {
                return BadRequest(new { message = "Kupon limiti tugagan." });
            }

            total -= discountAmount;
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
            CouponCode = appliedCouponCode,
            DiscountAmount = discountAmount,
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
        await _db.SaveChangesAsync();

        if (coupon != null)
        {
            _db.CouponRedemptions.Add(new CouponRedemption
            {
                CouponId = coupon.Id,
                UserId = userId.Value,
                OrderId = order.Id,
                DiscountAmount = discountAmount
            });
            await _db.SaveChangesAsync();
        }

        // Har 100 000 so'mga 1 ball — server ichida avtomatik qo'shiladi,
        // mijoz ballarni to'g'ridan-to'g'ri o'zgartira olmaydi.
        //
        // XAVFSIZLIK: "avval o'qib, qo'shib, keyin yozish" usuli ikkita poyga
        // holatiga yo'l qo'yardi — (1) ikkita buyurtma bir vaqtda kelsa, biri
        // ikkinchisining ball qo'shganini "yo'qotib" qo'yishi mumkin edi; (2)
        // foydalanuvchining BIRINCHI buyurtmasi ikki marta bir vaqtda kelsa,
        // ikkalasi ham UserPoints yozuvini yaratishga urinib, noyob cheklov
        // xatosi bilan BUYURTMANING O'ZI ham bekor bo'lib qolishi mumkin edi.
        // Shuning uchun bitta atomik "INSERT ... ON CONFLICT DO UPDATE" orqali,
        // mavjud bo'lsa qo'shiladi, bo'lmasa yaratiladi — hech qanday poyga yo'q.
        var pointsToAdd = (int)Math.Floor(total / 100_000m);
        if (pointsToAdd > 0)
        {
            var now = DateTime.UtcNow;
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""UserPoints"" (""UserId"", ""Balance"", ""TotalEarned"", ""YearPoints"", ""LastUpdated"")
                VALUES ({userId.Value}, {pointsToAdd}, {pointsToAdd}, {pointsToAdd}, {now})
                ON CONFLICT (""UserId"") DO UPDATE SET
                    ""Balance"" = ""UserPoints"".""Balance"" + {pointsToAdd},
                    ""TotalEarned"" = ""UserPoints"".""TotalEarned"" + {pointsToAdd},
                    ""YearPoints"" = ""UserPoints"".""YearPoints"" + {pointsToAdd},
                    ""LastUpdated"" = {now}");
        }

        return Ok(Project(order));
    }

    // Mijozning o'zi o'z buyurtmasini bekor qilishi uchun — admin ruxsati
    // shart emas, lekin faqat hali jo'natilmagan (pending/processing)
    // buyurtmani bekor qila oladi va faqat O'Z buyurtmasini.
    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Buyurtma topilmadi." });

        if (order.UserId != userId.Value)
        {
            return Forbid();
        }

        if (order.Status != "pending" && order.Status != "processing")
        {
            return BadRequest(new { message = "Bu buyurtmani endi bekor qilib bo'lmaydi." });
        }

        order.Status = "cancelled";
        await _db.SaveChangesAsync();

        // Buyurtma yaratilganda avtomatik qo'shilgan ballarni qaytarib olish —
        // aks holda mijoz "buyurtma berib-bekor qilish"ni takrorlab, cheksiz
        // ball hosil qilishi mumkin edi. GREATEST(...,0) — agar ball allaqachon
        // (masalan sovg'aga) sarflab bo'lingan bo'lsa, manfiy balansga tushirmaydi.
        var pointsToRevert = (int)Math.Floor(order.Total / 100_000m);
        if (pointsToRevert > 0)
        {
            var now = DateTime.UtcNow;
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""UserPoints"" SET
                    ""Balance"" = GREATEST(""Balance"" - {pointsToRevert}, 0),
                    ""TotalEarned"" = GREATEST(""TotalEarned"" - {pointsToRevert}, 0),
                    ""YearPoints"" = GREATEST(""YearPoints"" - {pointsToRevert}, 0),
                    ""LastUpdated"" = {now}
                WHERE ""UserId"" = {order.UserId}");
        }

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
        subtotal = o.Total + o.DiscountAmount,
        couponCode = o.CouponCode,
        discountAmount = o.DiscountAmount,
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
