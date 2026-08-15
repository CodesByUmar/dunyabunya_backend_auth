using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

/// <summary>
/// Odoo'dagi is_published=true mahsulotlarni davriy ravishda tortib, o'z bazamizga
/// (Products) ko'chirib qo'yadi — frontend Odoo'ga jonli murojaat qilmasdan, tez
/// javob olishi uchun. Har safar to'liq ro'yxat bilan solishtiriladi: yangi
/// mahsulotlar qo'shiladi, o'zgarganlari yangilanadi, endi publish qilinmaganlari
/// o'chiriladi.
/// </summary>
public class ProductSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ProductSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public ProductSyncBackgroundService(IServiceProvider services, IConfiguration config, ILogger<ProductSyncBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
        var minutes = double.TryParse(config["Product:SyncIntervalMinutes"], out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Product sync'da kutilmagan xato.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Ilova to'xtayotganda normal holat.
            }
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var odooProducts = scope.ServiceProvider.GetRequiredService<IOdooProductService>();

        var fresh = await odooProducts.GetPublishedProductsAsync();
        var freshIds = fresh.Select(p => p.OdooProductId).ToHashSet();

        var existing = await db.Products.ToListAsync(ct);
        var existingByOdooId = existing.ToDictionary(p => p.OdooProductId);

        var added = 0;
        var updated = 0;

        foreach (var dto in fresh)
        {
            if (existingByOdooId.TryGetValue(dto.OdooProductId, out var product))
            {
                product.OdooTemplateId = dto.OdooTemplateId;
                product.Name = dto.Name;
                product.DefaultCode = dto.DefaultCode;
                product.Barcode = dto.Barcode;
                product.Price = dto.Price;
                product.Cost = dto.Cost;
                product.CategoryName = dto.CategoryName;
                product.Brand = dto.Brand;
                product.InStock = dto.InStock;
                // ImageBase64'ga TEGILMAYDI — rasmni Odoo emas, admin panel (Superuser)
                // orqali qo'lda yuklaydi, sync uni har safar bo'sh bilan ustidan yozib
                // yubormasligi kerak.
                product.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.Products.Add(new Product
                {
                    OdooProductId = dto.OdooProductId,
                    OdooTemplateId = dto.OdooTemplateId,
                    Name = dto.Name,
                    DefaultCode = dto.DefaultCode,
                    Barcode = dto.Barcode,
                    Price = dto.Price,
                    Cost = dto.Cost,
                    CategoryName = dto.CategoryName,
                    Brand = dto.Brand,
                    InStock = dto.InStock
                });
                added++;
            }
        }

        var toRemove = existing.Where(p => !freshIds.Contains(p.OdooProductId)).ToList();
        if (toRemove.Count > 0) db.Products.RemoveRange(toRemove);

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product sync: {Fresh} ta Odoo'dan olindi ({Added} yangi, {Updated} yangilandi, {Removed} o'chirildi).",
            fresh.Count, added, updated, toRemove.Count);
    }
}
