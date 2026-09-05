using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Services;
using AuthApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuthApi.Tests.Services;

// SyncAsync — Odoo/Pending/Production uchburchagining "Odoo <-> Pending" va
// "Production <-> ko'rinish (IsPublishedInOdoo)" qismlarini tekshiradi
// (2026-09-05'da qayta ishlangan arxitektura). ApprovalStatus'ga sync HECH
// QACHON tegmasligi — bu yerdagi eng muhim invariant.
public class ProductSyncBackgroundServiceTests
{
    private sealed class FakeOdooProductService : IOdooProductService
    {
        public List<OdooProductDto> Products { get; set; } = new();
        public Task<List<OdooProductDto>> GetPublishedProductsAsync() => Task.FromResult(Products);
    }

    private sealed class NoOpHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new NotSupportedException("Ping URL sozlanmagan testlarda chaqirilmasligi kerak.");
    }

    private static (ProductSyncBackgroundService Service, TestDatabase Db, FakeOdooProductService Odoo) CreateService()
    {
        var test = TestDbContextFactory.Create();
        var fakeOdoo = new FakeOdooProductService();

        var services = new ServiceCollection();
        services.AddSingleton(test.Context);
        services.AddSingleton<IOdooProductService>(fakeOdoo);
        var provider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build(); // Healthchecks:OdooSyncPingUrl yo'q -> ping o'chirilgan
        var service = new ProductSyncBackgroundService(
            provider,
            new NoOpHttpClientFactory(),
            config,
            NullLogger<ProductSyncBackgroundService>.Instance);

        return (service, test, fakeOdoo);
    }

    private static OdooProductDto MakeDto(int odooProductId, string name = "Mahsulot", decimal price = 1000, string? categoryName = "Hammasi / Elektrika / Test") =>
        new(odooProductId, OdooTemplateId: odooProductId * 10, name, DefaultCode: null, Barcode: null, price, Cost: price / 2, categoryName, Brand: null, InStock: true);

    [Fact]
    public async Task SyncAsync_NewOdooProduct_IsAddedAsPendingAndPublished()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        odoo.Products = [MakeDto(100, name: "Yangi mahsulot")];

        await service.SyncAsync(CancellationToken.None);

        var product = test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100);
        Assert.Equal("pending", product.ApprovalStatus);
        Assert.True(product.IsPublishedInOdoo);
        Assert.Equal("Yangi mahsulot", product.Name);
    }

    [Fact]
    public async Task SyncAsync_ExistingProduct_UpdatesPriceAndKeepsApprovalStatus()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        test.Context.Products.Add(new Product
        {
            OdooProductId = 100,
            Name = "Eski nom",
            Price = 500,
            ApprovalStatus = "approved",
            IsPublishedInOdoo = true
        });
        await test.Context.SaveChangesAsync();

        odoo.Products = [MakeDto(100, name: "Yangilangan nom", price: 750)];
        await service.SyncAsync(CancellationToken.None);

        var product = test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100);
        Assert.Equal(750, product.Price);
        Assert.Equal("Yangilangan nom", product.Name);
        Assert.Equal("approved", product.ApprovalStatus); // sync bunga tegmaydi
    }

    [Fact]
    public async Task SyncAsync_NameOverridden_DoesNotOverwriteAdminEditedName()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        test.Context.Products.Add(new Product
        {
            OdooProductId = 100,
            Name = "Admin tahriri",
            NameOverridden = true,
            ApprovalStatus = "approved",
            IsPublishedInOdoo = true
        });
        await test.Context.SaveChangesAsync();

        odoo.Products = [MakeDto(100, name: "Odoo'dagi nom")];
        await service.SyncAsync(CancellationToken.None);

        var product = test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100);
        Assert.Equal("Admin tahriri", product.Name); // o'zgarmagan
        Assert.Equal("Odoo'dagi nom", product.OdooOriginalName); // orqa fonda saqlangan
    }

    [Fact]
    public async Task SyncAsync_MissingFewerThanThreeConsecutiveTimes_StaysPublished()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        test.Context.Products.Add(new Product { OdooProductId = 100, Name = "X", ApprovalStatus = "approved", IsPublishedInOdoo = true });
        await test.Context.SaveChangesAsync();

        odoo.Products = []; // 1-marta yo'q
        await service.SyncAsync(CancellationToken.None);
        Assert.True(test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100).IsPublishedInOdoo);

        await service.SyncAsync(CancellationToken.None); // 2-marta yo'q
        Assert.True(test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100).IsPublishedInOdoo);
    }

    [Fact]
    public async Task SyncAsync_MissingThreeConsecutiveTimes_BecomesUnpublishedButKeepsApproval()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        test.Context.Products.Add(new Product { OdooProductId = 100, Name = "X", ApprovalStatus = "approved", IsPublishedInOdoo = true });
        await test.Context.SaveChangesAsync();

        odoo.Products = [];
        await service.SyncAsync(CancellationToken.None); // 1
        await service.SyncAsync(CancellationToken.None); // 2
        await service.SyncAsync(CancellationToken.None); // 3 -> endi yashirinadi

        var product = test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100);
        Assert.False(product.IsPublishedInOdoo);
        Assert.Equal("approved", product.ApprovalStatus); // tasdiq holati saqlanadi
    }

    [Fact]
    public async Task SyncAsync_ReappearingAfterHidden_IsImmediatelyRepublishedWithoutDebounce()
    {
        var (service, test, odoo) = CreateService();
        using var _ = test;
        test.Context.Products.Add(new Product { OdooProductId = 100, Name = "X", ApprovalStatus = "approved", IsPublishedInOdoo = true });
        await test.Context.SaveChangesAsync();

        odoo.Products = [];
        await service.SyncAsync(CancellationToken.None);
        await service.SyncAsync(CancellationToken.None);
        await service.SyncAsync(CancellationToken.None);
        Assert.False(test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100).IsPublishedInOdoo);

        odoo.Products = [MakeDto(100, name: "X")]; // qaytadan paydo bo'ldi
        await service.SyncAsync(CancellationToken.None);

        Assert.True(test.Context.Products.AsNoTracking().Single(p => p.OdooProductId == 100).IsPublishedInOdoo);
    }
}
