using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

/// <summary>
/// Odoo'dagi is_published=true mahsulotlarni davriy ravishda tortib, o'z bazamizga
/// (Products) ko'chirib qo'yadi — frontend Odoo'ga jonli murojaat qilmasdan, tez
/// javob olishi uchun. Yangi mahsulotlar "pending" holatida qo'shiladi, mavjudlari
/// yangilanadi (narx/ombor/nom — admin tahrir qilmagan bo'lsa).
///
/// ApprovalStatus (Pending -> Production, ya'ni admin tasdig'i) bu servis
/// tomonidan HECH QACHON o'zgartirilmaydi — bu faqat admin qarori.
///
/// IsPublishedInOdoo (Production'ning ko'rinishi) esa Odoo bilan JONLI bog'liq:
/// admin Odoo'da is_published'ni o'chirsa, tasdiqlangan mahsulot ham ochiq
/// katalogdan darhol yashiriladi (ApprovalStatus o'zgarmasdan); qaytadan yoqsa,
/// qayta tasdiqlashsiz o'zi qaytadan ko'rinadi (q. SyncAsync ichidagi izoh).
/// </summary>
public class ProductSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;

    // healthchecks.io'ga sinxronizatsiya HAQIQATDA ishlayotganini bildiradigan
    // alohida signal — umumiy "dastur tirikmi" heartbeat'dan (HeartbeatBackgroundService)
    // FARQLI: agar Odoo bilan bog'lanish uzilsa (masalan API kalit yaroqsiz bo'lib
    // qolsa), dastur o'zi tinch ishlashda davom etadi, lekin bu signal kelmay qoladi —
    // shu orqali muammo soatlar/kunlar emas, daqiqalarda payqaladi. Bo'sh bo'lsa,
    // sokin o'chirilgan (hozircha URL yo'q, foydalanuvchi keyinroq qo'shadi).
    private readonly string? _odooSyncPingUrl;

    // MUHIM: Supabase'ga ulanishda vaqti-vaqti bilan (butun loyiha davomida
    // kuzatilgan) bir martalik, o'z-o'zidan keyingi siklda tuzaladigan uzilishlar
    // bo'lib turadi — bular haqiqiy Odoo muammosi emas. Agar HAR bitta shunday
    // blipda ham "/fail" yuborilsa, foydalanuvchiga daqiqada bir marta soxta
    // ogohlantirish keladi (sinab ko'rilgan, shovqin qildi). Shuning uchun faqat
    // MinConsecutiveFailuresToAlert marta KETMA-KET muvaffaqiyatsiz bo'lgandagina
    // "/fail" yuboriladi — bitta tasodifiy blip jim o'tkaziladi, lekin haqiqiy,
    // davom etadigan uzilish (masalan Odoo API kaliti yaroqsiz bo'lib qolgani kabi)
    // baribir bir necha daqiqada aniqlanadi.
    private int _consecutiveHealthcheckFailures;
    private const int MinConsecutiveFailuresToAlert = 3;

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

    // Mahsulot Odoo'ning "is_published=true" ro'yxatidan KETMA-KET necha marta
    // yo'q chiqqani — Odoo'ning search_read'i vaqti-vaqti bilan (aniqlanmagan
    // sababga ko'ra) bitta-ikkita mahsulotni tasodifiy tashlab qoldirishi mumkin
    // (q. OdooProductService izohi). Shuning uchun IsPublishedInOdoo'ni bitta
    // o'tkazib yuborilgandan keyinoq emas, faqat ketma-ket bir necha marta haqiqatan
    // yo'q bo'lgandagina "false" qilamiz — lekin qaytadan paydo bo'lsa, DARHOL
    // (kutmasdan) "true"ga qaytaramiz, chunki noto'g'ri ko'rsatmaslikdan ko'ra
    // bir muddat ko'rsatib qo'yish xavfsizroq.
    private readonly Dictionary<int, int> _missingStreak = new();
    private const int MinConsecutiveMissesToHide = 3;

    public ProductSyncBackgroundService(IServiceProvider services, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ProductSyncBackgroundService> logger)
    {
        _services = services;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        var minutes = double.TryParse(config["Product:SyncIntervalMinutes"], out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(minutes);
        _odooSyncPingUrl = config["Healthchecks:OdooSyncPingUrl"];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
                _consecutiveHealthcheckFailures = 0;
                await PingHealthcheckAsync(success: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Product sync'da kutilmagan xato.");
                _consecutiveHealthcheckFailures++;
                if (_consecutiveHealthcheckFailures >= MinConsecutiveFailuresToAlert)
                {
                    await PingHealthcheckAsync(success: false);
                }
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

    // healthchecks.io konvensiyasi: oddiy GET — muvaffaqiyat, "/fail" bilan GET —
    // xatolik (darhol ogohlantiradi, keyingi "grace period"ni kutmasdan). Ping
    // o'zi muvaffaqiyatsiz bo'lsa ham (internet yo'q va h.k.) sinxronizatsiyaning
    // o'ziga hech qanday ta'sir qilmaydi — shunchaki jim o'tkazib yuboriladi.
    private async Task PingHealthcheckAsync(bool success)
    {
        if (string.IsNullOrWhiteSpace(_odooSyncPingUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var url = success ? _odooSyncPingUrl : $"{_odooSyncPingUrl.TrimEnd('/')}/fail";
            await client.GetAsync(url);
        }
        catch
        {
            // Sokin o'tkazib yuboriladi — heartbeat servisidagi bilan bir xil naqsh.
        }
    }

    // internal (private emas) — AuthApi.Tests'dan to'g'ridan-to'g'ri chaqirib,
    // yangi/mavjud farqi va IsPublishedInOdoo debounce mantig'ini ExecuteAsync'ning
    // cheksiz sikli/Task.Delay'isiz sinash uchun (q. AuthApi.csproj'dagi InternalsVisibleTo).
    internal async Task SyncAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var odooProducts = scope.ServiceProvider.GetRequiredService<IOdooProductService>();

        var fresh = await odooProducts.GetPublishedProductsAsync();

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
                // Odoo'da hozir ham nashr etilgan ekan — darhol (kechiktirmasdan)
                // ko'rinadigan qilamiz, agar avval vaqtincha yashiringan bo'lsa ham.
                product.IsPublishedInOdoo = true;
                _missingStreak.Remove(product.OdooProductId);
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
                    ApprovalStatus = "pending",
                    IsPublishedInOdoo = true
                });
                added++;
            }
        }

        // MUHIM (arxitektura qarori, 2 bosqichda ishlaydi):
        // 1) Odoo <-> Pending: is_published yangi mahsulotni "pending" qilib
        //    qo'shadi, xolos. Pending -> Production o'tishi FAQAT admin qarori —
        //    ApprovalStatus'ga sync HECH QACHON tegmaydi (admin tasdig'i abadiy
        //    saqlanadi, Odoo uni "pending"ga qaytarib qo'ya olmaydi).
        // 2) Production <-> ko'rinish: admin tasdiqlagan mahsulot Odoo'da
        //    is_published=false qilinsa, IsPublishedInOdoo=false bo'ladi va
        //    mahsulot ochiq katalogdan (GET /api/products) DARHOL yashiriladi —
        //    lekin ApprovalStatus="approved" bo'lib qolaveradi. Admin Odoo'da
        //    qaytadan is_published=true qilsa, mahsulot QAYTA TASDIQLASHSIZ,
        //    o'zi qaytadan ko'rinadi (yuqoridagi tsiklda IsPublishedInOdoo=true
        //    qaytariladi).
        var freshIds = fresh.Select(p => p.OdooProductId).ToHashSet();
        foreach (var p in existing)
        {
            if (freshIds.Contains(p.OdooProductId)) continue; // yuqorida allaqachon true qilindi

            var streak = _missingStreak.GetValueOrDefault(p.OdooProductId) + 1;
            _missingStreak[p.OdooProductId] = streak;

            if (streak >= MinConsecutiveMissesToHide)
            {
                p.IsPublishedInOdoo = false;
            }
        }

        await db.SaveChangesAsync(ct);
        _lastKnownGoodCount = fresh.Count;

        _logger.LogInformation(
            "Product sync: {Fresh} ta Odoo'dan olindi ({Added} yangi, {Updated} yangilandi).",
            fresh.Count, added, updated);
    }
}
