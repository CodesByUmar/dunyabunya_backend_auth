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

    // Oxirgi muvaffaqiyatli sinxronizatsiyada nechta qator bo'lgani — DbContext'dan
    // MUSTAQIL, xotirada saqlanadi. Supabase connection pooling bilan bog'liq
    // (aniqlanmagan) sabablarga ko'ra ba'zan bir xil so'rov ikki marta ketma-ket
    // chaqirilganda ham bazadan (yoki Odoo'dan) noto'g'ri bo'sh natija qaytishi
    // kuzatildi — agar shunga ishonib qolsak, hammasi "yangi" deb hisoblanib,
    // ID'lar har safar o'zgarib, mahsulot havolalari (linklari) buzilib qolar edi.
    // Shu xotiradagi qiymat bilan solishtirish orqali bunday holatlarda hech
    // narsaga tegmay, keyingi davrga qoldiramiz.
    //
    // MUHIM: agar pasayish VAQTINCHALIK emas, HAQIQIY bo'lsa-chi (masalan
    // birov bazani chindan tozalab qo'ygan)? Shuning uchun buni ABADIY
    // o'tkazib yubormaymiz — ketma-ket bir necha marta shubhali holat
    // qaytarilsa, tizim "demak bu haqiqat" deb qabul qilib, o'zini tiklaydi.
    private int _lastKnownGoodCount = -1;
    private int _consecutiveSuspiciousDrops;
    private const int MaxConsecutiveSkips = 3;

    // Odoo'ning "is_published=true" so'rovi (search_read) vaqti-vaqti bilan, aniqlanmagan
    // sababga ko'ra, allaqachon nashr etilgan bitta-ikkita mahsulotni natijadan TASODIFIY
    // tashlab qoldirishi kuzatildi (326 tadan atigi 1-2 tasi — "ommaviy pasayish" himoyasi
    // buni ilg'amaydi, chunki u faqat 50%+ pasayishni tekshiradi). Bunga ishonib, mahsulotni
    // darhol "pending"ga o'tkazib yuborsak — admin tasdiqlagan mahsulot bir necha daqiqada
    // o'zi-o'zidan qaytib ketaverardi. Shuning uchun bitta mahsulot Odoo ro'yxatida KETMA-KET
    // bir necha marta yo'q bo'lgandagina (tasodifiy emasligi aniqlangach) "pending"ga o'tkaziladi.
    private readonly Dictionary<int, int> _missingStreak = new();
    private const int MinConsecutiveMissesToHide = 3;

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

        // VAQTINCHALIK DIAGNOSTIKA: EF natijasini xom SQL bilan solishtiramiz —
        // muammo EF/change-tracking'da yoki ulanish/sessiya darajasidami aniqlash uchun.
        if (existing.Count == 0)
        {
            try
            {
                var rawCount = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"Products\"").FirstAsync(ct);
                var connState = db.Database.GetDbConnection().State;
                var connStr = db.Database.GetConnectionString();
                var dbNameHint = connStr != null && connStr.Contains("Database=")
                    ? connStr.Substring(connStr.IndexOf("Database=")).Split(';')[0]
                    : "?";
                _logger.LogWarning(
                    "DIAGNOSTIKA-3: EF ToListAsync=0, xom SQL COUNT={RawCount}, ulanish holati={ConnState}, {DbHint}",
                    rawCount, connState, dbNameHint);
            }
            catch (Exception diagEx)
            {
                _logger.LogWarning(diagEx, "DIAGNOSTIKA-3: xom SQL tekshiruvi ham xato berdi.");
            }
        }

        // XAVFSIZLIK: agar avval muvaffaqiyatli sinxronlangan bo'lsak (ijobiy son
        // xotirada bor) va endi Odoo'dan kelgan ro'yxat YOKI bazadagi mavjud ro'yxat
        // to'satdan kutilganidan ANCHA kam ko'rinsa — buni vaqtinchalik noto'g'ri
        // o'qish deb hisoblab o'tkazib yuboramiz. Lekin bu holat ketma-ket bir necha
        // marta takrorlansa (haqiqiy o'zgarish bo'lishi mumkin), tizim o'zini tiklab,
        // baribir sinxronlaydi — abadiy tiqilib qolmaydi.
        var suspicious = _lastKnownGoodCount > 0 &&
            (existing.Count < _lastKnownGoodCount / 2 || fresh.Count < _lastKnownGoodCount / 2);

        if (suspicious && _consecutiveSuspiciousDrops < MaxConsecutiveSkips)
        {
            _consecutiveSuspiciousDrops++;
            _logger.LogWarning(
                "Product sync: kutilmagan pasayish (oldingi {Prev} ta, baza {Now} ta, Odoo {Fresh} ta) — ishonchsiz ({Attempt}/{Max}), bu davr o'tkazib yuborildi.",
                _lastKnownGoodCount, existing.Count, fresh.Count, _consecutiveSuspiciousDrops, MaxConsecutiveSkips);
            return;
        }

        if (suspicious)
        {
            _logger.LogWarning(
                "Product sync: pasayish {Attempt} marta ketma-ket takrorlandi — endi haqiqiy deb qabul qilinadi va sinxronlanadi.",
                _consecutiveSuspiciousDrops + 1);
        }

        _consecutiveSuspiciousDrops = 0;

        var existingByOdooId = existing.ToDictionary(p => p.OdooProductId);

        var added = 0;
        var updated = 0;

        foreach (var dto in fresh)
        {
            if (existingByOdooId.TryGetValue(dto.OdooProductId, out var product))
            {
                product.OdooTemplateId = dto.OdooTemplateId;
                // Admin panel orqali qo'lda tahrirlangan bo'lsa (NameOverridden/
                // CategoryNameOverridden), Odoo'dan kelgan qiymat bu maydonlarga
                // endi tegmaydi — admin tahriri doim ustun.
                if (!product.NameOverridden) product.Name = dto.Name;
                // Asl Odoo qiymati — admin tahriridan MUSTAQIL, har doim yangilanadi.
                product.OdooOriginalName = dto.Name;
                product.DefaultCode = dto.DefaultCode;
                product.Barcode = dto.Barcode;
                product.Price = dto.Price;
                product.Cost = dto.Cost;
                if (!product.CategoryNameOverridden) product.CategoryName = dto.CategoryName;
                product.OdooOriginalCategoryName = dto.CategoryName;
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
                    OdooOriginalName = dto.Name,
                    OdooOriginalCategoryName = dto.CategoryName,
                    Brand = dto.Brand,
                    InStock = dto.InStock,
                    ApprovalStatus = "pending"
                });
                added++;
            }
        }

        // Odoo'da is_published=false qilinsa (masalan omborda vaqtincha tugab, admin
        // vaqtincha o'chirib qo'ysa) — MAHSULOT O'CHIRILMAYDI, faqat qayta "pending"ga
        // o'tkaziladi (sayt katalogidan yashiriladi). Aks holda tavsif/rasm-galereya/
        // xususiyatlar/sharhlar har safar yo'qolib, qaytadan yoqilganda hammasi
        // yo'qotilgan bo'lardi. Admin qaytadan is_published qilsa, mahsulot "existing"
        // sifatida topilib, o'z holicha (pending) qoladi — qayta tasdiqlash kifoya.
        //
        // Lekin buni Odoo'dan bir martalik (ehtimol vaqtinchalik) yo'qligiga qarab
        // darhol qilmaymiz — ketma-ket MinConsecutiveMissesToHide marta yo'q bo'lsagina.
        var toHide = new List<Product>();
        foreach (var p in existing)
        {
            if (freshIds.Contains(p.OdooProductId))
            {
                _missingStreak.Remove(p.OdooProductId);
                continue;
            }

            if (p.ApprovalStatus == "pending") continue;

            var streak = _missingStreak.GetValueOrDefault(p.OdooProductId) + 1;
            _missingStreak[p.OdooProductId] = streak;

            if (streak >= MinConsecutiveMissesToHide)
            {
                toHide.Add(p);
                _missingStreak.Remove(p.OdooProductId);
            }
            else
            {
                _logger.LogInformation(
                    "Product sync: OdooProductId={OdooProductId} Odoo ro'yxatida yo'q ({Streak}/{Max} marta ketma-ket) — hali \"pending\"ga o'tkazilmadi, ehtimol vaqtinchalik.",
                    p.OdooProductId, streak, MinConsecutiveMissesToHide);
            }
        }

        foreach (var p in toHide) p.ApprovalStatus = "pending";

        await db.SaveChangesAsync(ct);
        _lastKnownGoodCount = fresh.Count;

        _logger.LogInformation(
            "Product sync: {Fresh} ta Odoo'dan olindi ({Added} yangi, {Updated} yangilandi, {Hidden} qayta pending qilindi).",
            fresh.Count, added, updated, toHide.Count);
    }
}
