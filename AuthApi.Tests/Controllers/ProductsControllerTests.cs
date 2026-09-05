using AuthApi.Controllers;
using AuthApi.Models;
using AuthApi.Services;
using AuthApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // --- SetApprovalStatus ---

    [Fact]
    public async Task SetApprovalStatus_ApproveWhenNotPublishedInOdoo_ReturnsBadRequestAndLeavesStatusUnchanged()
    {
        var product = MakeProduct();
        product.IsPublishedInOdoo = false;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetApprovalStatus(1, new ProductApprovalDto { Status = "approved" });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("pending", test.Context.Products.AsNoTracking().Single(p => p.Id == 1).ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatus_ApproveWhenPublishedInOdoo_Succeeds()
    {
        var product = MakeProduct();
        product.IsPublishedInOdoo = true;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetApprovalStatus(1, new ProductApprovalDto { Status = "approved" });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("approved", test.Context.Products.AsNoTracking().Single(p => p.Id == 1).ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatus_RejectWhenNotPublishedInOdoo_StillSucceeds()
    {
        // Rad etish har doim ishlashi kerak — faqat "approved" bloklanadi.
        var product = MakeProduct();
        product.IsPublishedInOdoo = false;
        using var test = await SeedAsync(product);
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetApprovalStatus(1, new ProductApprovalDto { Status = "rejected" });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("rejected", test.Context.Products.AsNoTracking().Single(p => p.Id == 1).ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatus_InvalidStatus_ReturnsBadRequest()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetApprovalStatus(1, new ProductApprovalDto { Status = "unknown" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetApprovalStatus_ProductNotFound_ReturnsNotFound()
    {
        using var test = await SeedAsync(MakeProduct());
        var controller = new ProductsController(test.Context, new ProductCategoryService(test.Context));

        var result = await controller.SetApprovalStatus(999, new ProductApprovalDto { Status = "approved" });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
