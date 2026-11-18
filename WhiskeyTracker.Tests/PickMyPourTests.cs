using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Collections;
using Xunit;

namespace WhiskeyTracker.Tests;

public class PickMyPourTests : TestBase
{
    private async Task<AppDbContext> SeedAsync(string userId)
    {
        var context = GetInMemoryContext();
        context.Users.Add(new ApplicationUser { Id = userId, UserName = "u", Email = "u@x.com" });
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Pick Me", Distillery = "D" });
        context.Collections.AddRange(new Collection { Id = 10, Name = "Mine" }, new Collection { Id = 20, Name = "Other" });
        context.CollectionMembers.Add(new CollectionMember { CollectionId = 10, UserId = userId, Role = CollectionRole.Owner });
        context.Bottles.AddRange(
            new Bottle { Id = 1, WhiskeyId = 1, CollectionId = 10, Status = BottleStatus.Opened },
            new Bottle { Id = 2, WhiskeyId = 1, CollectionId = 10, Status = BottleStatus.Empty },
            new Bottle { Id = 3, WhiskeyId = 1, CollectionId = 20, Status = BottleStatus.Full });
        await context.SaveChangesAsync();
        return context;
    }

    private DetailsModel Create(AppDbContext context, string userId)
    {
        var page = new DetailsModel(context, GetMockUserManager(context));
        SetMockUser(page, userId);
        return page;
    }

    [Fact]
    public async Task PickMyPour_ReturnsOnlyNonEmptyBottleFromThisCollection()
    {
        using var context = await SeedAsync("u1");
        var page = Create(context, "u1");

        for (var i = 0; i < 20; i++)
        {
            var result = await page.OnPostPickMyPourAsync(10);
            Assert.IsType<PageResult>(result);
            Assert.Equal(1, page.PickedBottle!.Id);
        }
    }

    [Fact]
    public async Task PickMyPour_NoEligibleBottles_ReturnsNoPick()
    {
        using var context = await SeedAsync("u1");
        context.Bottles.Remove(context.Bottles.Find(1)!);
        await context.SaveChangesAsync();
        var page = Create(context, "u1");

        await page.OnPostPickMyPourAsync(10);

        Assert.True(page.PickAttempted);
        Assert.Null(page.PickedBottle);
    }

    [Fact]
    public async Task PickMyPour_NonMember_IsForbidden()
    {
        using var context = await SeedAsync("u1");
        var page = Create(context, "stranger");

        var result = await page.OnPostPickMyPourAsync(10);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(page.PickedBottle);
    }
}
