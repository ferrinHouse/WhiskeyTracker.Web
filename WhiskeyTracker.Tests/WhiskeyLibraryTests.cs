using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using System.Security.Claims;
using Xunit;

namespace WhiskeyTracker.Tests;

public class WhiskeyLibraryTests : TestBase
{
    [Fact]
    public async Task Index_OnGet_FiltersByShowOnlyMyCollection()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "user1";
        
        var whiskeyOwned = new Whiskey { Name = "Owned Whiskey", Distillery = "Dist A" };
        var whiskeyNotOwned = new Whiskey { Name = "Not Owned Whiskey", Distillery = "Dist B" };
        context.Whiskies.AddRange(whiskeyOwned, whiskeyNotOwned);
        
        var collection = new Collection { Name = "My Collection" };
        context.Collections.Add(collection);
        context.CollectionMembers.Add(new CollectionMember { UserId = userId, Collection = collection, Role = CollectionRole.Owner });
        
        var bottle = new Bottle { Whiskey = whiskeyOwned, Collection = collection, Status = BottleStatus.Opened };
        context.Bottles.Add(bottle);
        
        await context.SaveChangesAsync();

        var legacyService = new WhiskeyTracker.Web.Services.LegacyMigrationService(context);
        var pageModel = new IndexModel(context, legacyService);
        SetMockUser(pageModel, userId);
        pageModel.ShowOnlyMyCollection = true;

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        Assert.Single(pageModel.Whiskies);
        Assert.Equal("Owned Whiskey", pageModel.Whiskies[0].Name);
    }

    [Fact]
    public async Task Index_OnGet_FiltersByStatus()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "user1";
        
        var whiskeyOpened = new Whiskey { Name = "Opened Whiskey", Distillery = "Dist A" };
        var whiskeyFull = new Whiskey { Name = "Full Whiskey", Distillery = "Dist B" };
        context.Whiskies.AddRange(whiskeyOpened, whiskeyFull);
        
        var collection = new Collection { Name = "My Collection" };
        context.Collections.Add(collection);
        context.CollectionMembers.Add(new CollectionMember { UserId = userId, Collection = collection, Role = CollectionRole.Owner });
        
        context.Bottles.AddRange(
            new Bottle { Whiskey = whiskeyOpened, Collection = collection, Status = BottleStatus.Opened },
            new Bottle { Whiskey = whiskeyFull, Collection = collection, Status = BottleStatus.Full }
        );
        
        await context.SaveChangesAsync();

        var legacyService = new WhiskeyTracker.Web.Services.LegacyMigrationService(context);
        var pageModel = new IndexModel(context, legacyService);
        SetMockUser(pageModel, userId);
        pageModel.Statuses.Add(BottleStatus.Opened);

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        // Note: Logic is "show whiskies that have at least one bottle with this status in my collection"
        Assert.Single(pageModel.Whiskies);
        Assert.Equal("Opened Whiskey", pageModel.Whiskies[0].Name);
    }

    private record StockScenario(
        Collection Alpha, Collection Bravo, Collection Foreign,
        Whiskey InAlpha, Whiskey InBravo, Whiskey EmptyInAlpha, Whiskey ZeroVolumeInAlpha,
        Whiskey OnlyInForeign, Whiskey NoCollection, Whiskey NoBottles);

    private static async Task<StockScenario> SeedStockScenarioAsync(AppDbContext context, string userId)
    {
        var alpha = new Collection { Name = "Alpha" };
        var bravo = new Collection { Name = "Bravo" };
        var foreign = new Collection { Name = "Someone Else's" };
        context.Collections.AddRange(alpha, bravo, foreign);
        context.CollectionMembers.AddRange(
            new CollectionMember { UserId = userId, Collection = alpha, Role = CollectionRole.Owner },
            new CollectionMember { UserId = userId, Collection = bravo, Role = CollectionRole.Viewer },
            new CollectionMember { UserId = "other-user", Collection = foreign, Role = CollectionRole.Owner });

        var inAlpha = new Whiskey { Name = "In Alpha", Distillery = "D" };
        var inBravo = new Whiskey { Name = "In Bravo", Distillery = "D" };
        var emptyInAlpha = new Whiskey { Name = "Empty In Alpha", Distillery = "D" };
        var zeroVolume = new Whiskey { Name = "Zero Volume In Alpha", Distillery = "D" };
        var onlyInForeign = new Whiskey { Name = "Only In Foreign", Distillery = "D" };
        var noCollection = new Whiskey { Name = "No Collection", Distillery = "D" };
        var noBottles = new Whiskey { Name = "No Bottles", Distillery = "D" };
        context.Whiskies.AddRange(inAlpha, inBravo, emptyInAlpha, zeroVolume, onlyInForeign, noCollection, noBottles);

        context.Bottles.AddRange(
            new Bottle { Whiskey = inAlpha, Collection = alpha, Status = BottleStatus.Full },
            new Bottle { Whiskey = inBravo, Collection = bravo, Status = BottleStatus.Opened, CurrentVolumeMl = 200 },
            new Bottle { Whiskey = emptyInAlpha, Collection = alpha, Status = BottleStatus.Empty, CurrentVolumeMl = 0 },
            new Bottle { Whiskey = zeroVolume, Collection = alpha, Status = BottleStatus.Opened, CurrentVolumeMl = 0 },
            new Bottle { Whiskey = onlyInForeign, Collection = foreign, Status = BottleStatus.Full },
            new Bottle { Whiskey = noCollection, Collection = null, Status = BottleStatus.Full });
        await context.SaveChangesAsync();

        return new StockScenario(alpha, bravo, foreign, inAlpha, inBravo, emptyInAlpha, zeroVolume, onlyInForeign, noCollection, noBottles);
    }

    [Fact]
    public async Task GetInStockWhiskeyIds_CountsOnlyUsableBottlesInGivenCollections()
    {
        using var context = GetInMemoryContext();
        var s = await SeedStockScenarioAsync(context, "user1");

        var both = await context.GetInStockWhiskeyIdsAsync(new[] { s.Alpha.Id, s.Bravo.Id });
        var alphaOnly = await context.GetInStockWhiskeyIdsAsync(new[] { s.Alpha.Id });
        var none = await context.GetInStockWhiskeyIdsAsync(Array.Empty<int>());

        Assert.True(both.SetEquals(new[] { s.InAlpha.Id, s.InBravo.Id }));
        Assert.True(alphaOnly.SetEquals(new[] { s.InAlpha.Id }));
        Assert.Empty(none);
    }

    [Fact]
    public async Task Index_OnGet_ListsAllWhiskies_ButStockComesOnlyFromMyCollections()
    {
        using var context = GetInMemoryContext();
        var s = await SeedStockScenarioAsync(context, "user1");
        var pageModel = new IndexModel(context, new WhiskeyTracker.Web.Services.LegacyMigrationService(context));
        SetMockUser(pageModel, "user1");

        await pageModel.OnGetAsync();

        Assert.Equal(7, pageModel.Whiskies.Count);
        Assert.True(pageModel.InStockWhiskeyIds.SetEquals(new[] { s.InAlpha.Id, s.InBravo.Id }));
        Assert.DoesNotContain(s.OnlyInForeign.Id, pageModel.InStockWhiskeyIds);
        Assert.DoesNotContain(s.NoCollection.Id, pageModel.InStockWhiskeyIds);
    }

    [Fact]
    public async Task Index_OnGet_CollectionId_LimitsListAndStockToThatCollection()
    {
        using var context = GetInMemoryContext();
        var s = await SeedStockScenarioAsync(context, "user1");
        var pageModel = new IndexModel(context, new WhiskeyTracker.Web.Services.LegacyMigrationService(context));
        SetMockUser(pageModel, "user1");
        pageModel.CollectionId = s.Alpha.Id;

        await pageModel.OnGetAsync();

        var listed = pageModel.Whiskies.Select(w => w.Name).ToHashSet();
        Assert.True(listed.SetEquals(new[] { "In Alpha", "Empty In Alpha", "Zero Volume In Alpha" }));
        Assert.True(pageModel.InStockWhiskeyIds.SetEquals(new[] { s.InAlpha.Id }));
    }

    [Fact]
    public async Task Index_OnGet_CollectionUserDoesNotBelongTo_IsIgnored()
    {
        using var context = GetInMemoryContext();
        var s = await SeedStockScenarioAsync(context, "user1");
        var pageModel = new IndexModel(context, new WhiskeyTracker.Web.Services.LegacyMigrationService(context));
        SetMockUser(pageModel, "user1");
        pageModel.CollectionId = s.Foreign.Id;

        await pageModel.OnGetAsync();

        Assert.Null(pageModel.CollectionId);
        Assert.Equal(7, pageModel.Whiskies.Count);
        Assert.True(pageModel.InStockWhiskeyIds.SetEquals(new[] { s.InAlpha.Id, s.InBravo.Id }));
    }
}
