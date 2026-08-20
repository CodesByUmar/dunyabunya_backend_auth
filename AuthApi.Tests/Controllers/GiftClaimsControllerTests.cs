using AuthApi.Controllers;
using AuthApi.Models;
using AuthApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthApi.Tests.Controllers;

// GiftClaimsController.ClaimGift — bu yerdagi eng muhim xatti-harakat: ball
// yechish atomik SQL UPDATE orqali (ExecuteUpdateAsync), "avval o'qib, keyin
// yozish" emas — shuning uchun ikkita so'rov bir vaqtda kelsa ham bitta ball
// ikki marta sarflanmaydi. Shu testlar aynan shu kafolatni tekshiradi.
public class GiftClaimsControllerTests
{
    private const int UserId = 1;
    private const int CampaignId = 1;
    private const int TierId = 1;
    private const int TierPoints = 100;

    private static async Task<TestDatabase> SeedAsync(int startingBalance)
    {
        var test = TestDbContextFactory.Create();
        var db = test.Context;

        db.GiftCampaigns.Add(new GiftCampaign
        {
            Id = CampaignId,
            Name = "Test",
            IsActive = true,
            SelectionStartDate = DateTime.UtcNow.AddDays(-1),
            SelectionEndDate = DateTime.UtcNow.AddDays(1)
        });
        db.GiftTiers.Add(new GiftTier { Id = TierId, Points = TierPoints, Title = "Sovg'a" });
        db.UserPoints.Add(new UserPoints { UserId = UserId, Balance = startingBalance, TotalEarned = startingBalance });

        await db.SaveChangesAsync();
        return test;
    }

    [Fact]
    public async Task ClaimGift_SufficientBalance_SucceedsAndDeductsPoints()
    {
        using var test = await SeedAsync(startingBalance: 150);
        var db = test.Context;
        var controller = new GiftClaimsController(db) { ControllerContext = TestDbContextFactory.ControllerContextFor(UserId) };

        var result = await controller.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });

        Assert.IsType<OkObjectResult>(result);
        var balance = db.UserPoints.AsNoTracking().Single(p => p.UserId == UserId).Balance;
        Assert.Equal(50, balance);
    }

    [Fact]
    public async Task ClaimGift_InsufficientBalance_ReturnsBadRequestAndLeavesBalanceUnchanged()
    {
        using var test = await SeedAsync(startingBalance: 50); // TierPoints (100) dan kam
        var db = test.Context;
        var controller = new GiftClaimsController(db) { ControllerContext = TestDbContextFactory.ControllerContextFor(UserId) };

        var result = await controller.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });

        Assert.IsType<BadRequestObjectResult>(result);
        var balance = db.UserPoints.AsNoTracking().Single(p => p.UserId == UserId).Balance;
        Assert.Equal(50, balance); // o'zgarmagan
        Assert.Empty(db.UserGiftClaims); // yozuv yaratilmagan
    }

    [Fact]
    public async Task ClaimGift_ExactlyEnoughBalance_SucceedsAndBalanceReachesZero()
    {
        using var test = await SeedAsync(startingBalance: TierPoints);
        var db = test.Context;
        var controller = new GiftClaimsController(db) { ControllerContext = TestDbContextFactory.ControllerContextFor(UserId) };

        var result = await controller.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, db.UserPoints.AsNoTracking().Single(p => p.UserId == UserId).Balance);
    }

    [Fact]
    public async Task ClaimGift_SecondClaimAfterFirstDepletesBalance_IsRejected()
    {
        // Ikki ketma-ket so'rov (poyga holatini to'liq simulyatsiya qilmasa ham,
        // "avval o'qib qo'yilgan eski balansga" ishonib qolinmasligini tekshiradi).
        using var test = await SeedAsync(startingBalance: TierPoints);
        var db = test.Context;
        var controllerContext = TestDbContextFactory.ControllerContextFor(UserId);
        var controller1 = new GiftClaimsController(db) { ControllerContext = controllerContext };
        var controller2 = new GiftClaimsController(db) { ControllerContext = controllerContext };

        var first = await controller1.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });
        var second = await controller2.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });

        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<BadRequestObjectResult>(second);
        Assert.Equal(0, db.UserPoints.AsNoTracking().Single(p => p.UserId == UserId).Balance);
        Assert.Single(db.UserGiftClaims); // faqat bitta sovg'a olindi
    }

    [Fact]
    public async Task ClaimGift_InactiveCampaign_ReturnsBadRequest()
    {
        using var test = await SeedAsync(startingBalance: 150);
        var db = test.Context;
        db.GiftCampaigns.Single().IsActive = false;
        await db.SaveChangesAsync();
        var controller = new GiftClaimsController(db) { ControllerContext = TestDbContextFactory.ControllerContextFor(UserId) };

        var result = await controller.ClaimGift(new CreateGiftClaimDto { CampaignId = CampaignId, GiftTierId = TierId, Quantity = 1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
