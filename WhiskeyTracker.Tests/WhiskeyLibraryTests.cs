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
        pageModel.Status = BottleStatus.Opened;

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        // Note: Logic is "show whiskies that have at least one bottle with this status in my collection"
        Assert.Single(pageModel.Whiskies);
        Assert.Equal("Opened Whiskey", pageModel.Whiskies[0].Name);
    }
}
