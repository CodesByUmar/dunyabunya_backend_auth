using AuthApi.Controllers;
using AuthApi.Models;
using AuthApi.Services;
using AuthApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AuthApi.Tests.Controllers;

// UpdateProductDetails va SetApprovalStatus — 2026-09-05'da tuzatilgan ikkita real
// production bug (Subkategoriya-yolg'iz tahriri e'tiborsiz qolishi, va Odoo'da
// is_published=false bo'lgan mahsulotni tasdiqlab bo'lish) shu yerda qamrab olinadi,
// kelajakda qaytadan sinmasligi uchun.
public class ProductsControllerTests
{
    private const string CategorySlug = "santekhnika";
    // Kichik harflar bilan atayin saqlangan: SQLite'ning LOWER() funksiyasi kirill
    // harflarini to'g'ri kichiklashtirmaydi (faqat ASCII), production'dagi PostgreSQL'dan
    // farqli — shu bilan ".ToLower()" solishtiruvi DB tarafida hech narsani o'zgartirmaydigan
    // holatda testlanadi, C# tarafidagi (client-side, chinakam Unicode-aware) ".ToLower()"
    // esa pastda mixed-case kirim orqali alohida tekshiriladi.
    private const string SubcategoryNameRu = "трубы и фитинги";
    private const string SubcategorySlug = "truby-i-fitingi";

    private static async Task<TestDatabase> SeedAsync(Product product)
    {
        var test = TestDbContextFactory.Create();
        var db = test.Context;

        db.Categories.Add(new Category
        {
            Id = 1,
            NameRu = "Сантехника",
            Slug = CategorySlug,
            Subcategories = new List<Subcategory>
            {
                new() { CategoryId = 1, NameRu = SubcategoryNameRu, Slug = SubcategorySlug, Order = 0 }
            }
        });
        db.Products.Add(product);

        await db.SaveChangesAsync();
        return test;
    }

    private static Product MakeProduct(string? categoryName = null) => new()
    {
        Id = 1,
        OdooProductId = 1,
        Name = "Test mahsulot",
        CategoryName = categoryName,
        ApprovalStatus = "pending",
        IsPublishedInOdoo = true
    };

    // --- UpdateProductDetails ---

    [Fact]
    public async Task UpdateProductDetails_CategoryAndSubcategory_ReconstructsCategoryNameAndSetsSlug()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Category = "Santexnika",
            // Aralash registrda — case-insensitive solishtiruv (client-side ToLower)
            // haqiqatan ishlashini tekshiradi.
            Subcategory = "Трубы И Фитинги"
        });

        Assert.IsType<OkObjectResult>(result);
        var product = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Equal($"Hammasi / Muhandislik tizimlari / {SubcategoryNameRu}", product.CategoryName);
        Assert.Equal(SubcategorySlug, product.SubcategorySlug);
        Assert.True(product.CategoryNameOverridden);
    }

    [Fact]
    public async Task UpdateProductDetails_SubcategoryOnly_UsesProductsCurrentCategory()
    {
        // Bug (2026-09-05'gacha): Category yuborilmasa, bu blok umuman ishga
        // tushmas edi va Subcategory e'tiborsiz qoldirilardi.
        using var test = await SeedAsync(MakeProduct(categoryName: "Hammasi / Muhandislik tizimlari / Eski"));
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Subcategory = SubcategorySlug // slug shaklida ham qabul qilinishi kerak
        });

        Assert.IsType<OkObjectResult>(result);
        var product = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Equal($"Hammasi / Muhandislik tizimlari / {SubcategoryNameRu}", product.CategoryName);
        Assert.Equal(SubcategorySlug, product.SubcategorySlug);
    }

    [Fact]
    public async Task UpdateProductDetails_SubcategoryOnly_WithoutExistingValidCategory_ReturnsBadRequest()
    {
        using var test = await SeedAsync(MakeProduct(categoryName: null));
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Subcategory = SubcategorySlug
        });

        Assert.IsType<BadRequestObjectResult>(result);
        var product = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Null(product.SubcategorySlug); // hech narsa yozilmagan
    }

    [Fact]
    public async Task UpdateProductDetails_UnknownCategory_ReturnsBadRequest()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Category = "Mavjud bo'lmagan kategoriya",
            Subcategory = SubcategoryNameRu
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateProductDetails_UnknownSubcategoryForValidCategory_ReturnsBadRequest()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Category = "Santexnika",
            Subcategory = "Mavjud bo'lmagan subkategoriya"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        var product = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Null(product.SubcategorySlug);
    }

    [Fact]
    public async Task UpdateProductDetails_EmptySubcategory_ReturnsBadRequest()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto
        {
            Category = "Santexnika",
            Subcategory = "   "
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateProductDetails_NameOnly_DoesNotTouchCategory()
    {
        using var test = await SeedAsync(MakeProduct(categoryName: "Hammasi / Muhandislik tizimlari / Eski"));
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto { Name = "Yangi nom" });

        Assert.IsType<OkObjectResult>(result);
        var product = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Equal("Yangi nom", product.Name);
        Assert.Equal("Hammasi / Muhandislik tizimlari / Eski", product.CategoryName);
        Assert.Null(product.SubcategorySlug);
    }

    [Fact]
    public async Task UpdateProductDetails_ProductNotFound_ReturnsNotFound()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(999, new UpdateProductDetailsDto { Name = "X" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- SetOnlineStatus (2026-09-12'gacha SetApprovalStatus deb atalgan) ---

    [Fact]
    public async Task SetOnlineStatus_OnlineIsAlwaysBlocked_RegardlessOfOdooPublishState()
    {
        // Tezkor tugma orqali Online qilib bo'lmaydi — buni faqat tahrirlash
        // oynasi (UpdateProductDetails) qila oladi. Bu — Odoo holatidan mustaqil.
        var product = MakeProduct();
        product.IsPublishedInOdoo = true;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetOnlineStatus(1, new UpdateOnlineStatusDto { IsOnline = true });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(test.Context.Products.AsNoTracking().Single(p => p.Id == 1).IsOnline);
    }

    [Fact]
    public async Task SetOnlineStatus_OfflineWhenNotPublishedInOdoo_StillSucceeds()
    {
        // Offline qilish har doim ishlashi kerak — faqat Online'ga o'tish bloklanadi.
        var product = MakeProduct();
        product.IsOnline = true;
        product.IsPublishedInOdoo = false;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetOnlineStatus(1, new UpdateOnlineStatusDto { IsOnline = false });

        Assert.IsType<OkObjectResult>(result);
        Assert.False(test.Context.Products.AsNoTracking().Single(p => p.Id == 1).IsOnline);
    }

    [Fact]
    public async Task SetOnlineStatus_ProductNotFound_ReturnsNotFound()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetOnlineStatus(999, new UpdateOnlineStatusDto { IsOnline = true });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- UpdateProductDetails'dagi IsOnline dropdown (bitta "Saqlash" bilan) ---

    [Fact]
    public async Task UpdateProductDetails_IsOnlineTrueWhenNotPublishedInOdoo_ReturnsBadRequestAndLeavesUnchanged()
    {
        var product = MakeProduct();
        product.IsPublishedInOdoo = false;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto { IsOnline = true });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(test.Context.Products.AsNoTracking().Single(p => p.Id == 1).IsOnline);
    }

    [Fact]
    public async Task UpdateProductDetails_IsOnlineTrueWhenPublishedInOdoo_SavesTogetherWithOtherFields()
    {
        var product = MakeProduct();
        product.IsPublishedInOdoo = true;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto { Name = "Yangi nom", IsOnline = true });

        Assert.IsType<OkObjectResult>(result);
        var saved = test.Context.Products.AsNoTracking().Single(p => p.Id == 1);
        Assert.Equal("Yangi nom", saved.Name);
        Assert.True(saved.IsOnline);
    }

    [Fact]
    public async Task UpdateProductDetails_IsOnlineNotProvided_LeavesOnlineStatusUnchanged()
    {
        var product = MakeProduct();
        product.IsOnline = true;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.UpdateProductDetails(1, new UpdateProductDetailsDto { Name = "Yangi nom" });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(test.Context.Products.AsNoTracking().Single(p => p.Id == 1).IsOnline);
    }

    // --- GetOdooInfo (gibrid yechim: ID bo'yicha jonli Odoo tekshiruvi) ---

    private sealed class FakeOdooProductService : IOdooProductService
    {
        public OdooCurrentProductInfo? InfoToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public int CallCount { get; private set; }

        public Task<List<OdooProductDto>> GetPublishedProductsAsync() => Task.FromResult(new List<OdooProductDto>());

        public Task<OdooCurrentProductInfo?> GetProductInfoByIdAsync(int odooProductId)
        {
            CallCount++;
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(InfoToReturn);
        }
    }

    private static ProductsController CreateController(TestDatabase test, FakeOdooProductService? odoo = null) =>
        new(test.Context, new ProductCategoryService(test.Context), odoo, new MemoryCache(new MemoryCacheOptions()));

    private static OdooCurrentProductInfo MakeOdooInfo(int odooProductId = 1, string name = "Odoo'dagi hozirgi nom") =>
        new(odooProductId, OdooTemplateId: odooProductId * 10, name, DefaultCode: "0001", Barcode: null,
            Price: 1500, Cost: 1000, CategoryName: "Электрика", Brand: "AVR", InStock: true, IsPublishedInOdoo: true);

    [Fact]
    public async Task GetOdooInfo_ProductNotFoundLocally_ReturnsNotFound()
    {
        using var test = await SeedAsync(MakeProduct());
        // Odoo servisiz ham — lokal qator yo'qligi birinchi tekshiriladi (404).
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.GetOdooInfo(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetOdooInfo_OdooIntegrationMissing_Returns503()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.GetOdooInfo(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetOdooInfo_Success_ReturnsLiveInfoWithMarketplaceNames()
    {
        var product = MakeProduct();
        product.OdooProductId = 42;
        product.OdooOriginalName = "Birinchi kelgan nom";
        using var test = await SeedAsync(product);
        var odoo = new FakeOdooProductService { InfoToReturn = MakeOdooInfo(odooProductId: 42) };
        var controller = CreateController(test, odoo);

        var result = await controller.GetOdooInfo(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ok.Value;
        Assert.NotNull(payload);
        var cachedFrom = (string?)payload!.GetType().GetProperty("cachedFrom")?.GetValue(payload);
        Assert.Equal("odoo", cachedFrom);
        var odooInfo = (OdooCurrentProductInfo?)payload.GetType().GetProperty("odoo")?.GetValue(payload);
        Assert.NotNull(odooInfo);
        Assert.Equal("Odoo'dagi hozirgi nom", odooInfo!.Name);
        Assert.Equal("Test mahsulot", payload.GetType().GetProperty("marketplaceName")?.GetValue(payload));
        Assert.Equal("Birinchi kelgan nom", payload.GetType().GetProperty("odooOriginalName")?.GetValue(payload));
        Assert.Equal(1, odoo.CallCount); // aynan bitta jonli so'rov ketdi
    }

    [Fact]
    public async Task GetOdooInfo_SecondCallWithinCacheWindow_HitsOdooOnlyOnce()
    {
        using var test = await SeedAsync(MakeProduct());
        var odoo = new FakeOdooProductService { InfoToReturn = MakeOdooInfo() };
        var controller = CreateController(test, odoo);

        var first = await controller.GetOdooInfo(1);
        var second = await controller.GetOdooInfo(1);

        Assert.Equal(1, odoo.CallCount); // ikkinchi chaqiruv cache'dan
        var firstPayload = Assert.IsType<OkObjectResult>(first).Value!;
        var secondPayload = Assert.IsType<OkObjectResult>(second).Value!;
        Assert.Equal("odoo", (string?)firstPayload.GetType().GetProperty("cachedFrom")?.GetValue(firstPayload));
        Assert.Equal("cache", (string?)secondPayload.GetType().GetProperty("cachedFrom")?.GetValue(secondPayload));
    }

    [Fact]
    public async Task GetOdooInfo_OdooThrows_Returns503()
    {
        using var test = await SeedAsync(MakeProduct());
        var odoo = new FakeOdooProductService { ExceptionToThrow = new InvalidOperationException("Odoo down") };
        var controller = CreateController(test, odoo);

        var result = await controller.GetOdooInfo(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetOdooInfo_ProductDeletedInOdoo_ReturnsNotFound()
    {
        using var test = await SeedAsync(MakeProduct());
        var odoo = new FakeOdooProductService { InfoToReturn = null }; // Odoo'da yo'q
        var controller = CreateController(test, odoo);

        var result = await controller.GetOdooInfo(1);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
