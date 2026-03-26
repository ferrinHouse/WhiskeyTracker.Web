using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Admin;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace WhiskeyTracker.Tests;

public class AdminIndexTests
{
    private AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task OnGet_CalculatesAllStatsAndOrphans()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        
        // 1. Users
        // In-Memory DB doesn't automatically have Identity users, but we can mock them if needed.
        // For simplicity, let's just add some basic data.
        
        // 2. Whiskies
        context.Whiskies.Add(new Whiskey { Name = "W1" });
        
        // 3. Bottles
        context.Bottles.Add(new Bottle { WhiskeyId = 1, CurrentVolumeMl = 750 });
        
        // 4. Tags
        context.Tags.AddRange(
            new Tag { Name = "T1", IsApproved = true },
            new Tag { Name = "T2", IsApproved = false }
        );

        // 5. Orphans
        // A bottle with no collection and no user (if configured to require one)
        context.Bottles.Add(new Bottle { Id = 2, WhiskeyId = 1, CollectionId = 999 }); // CollectionId 999 doesn't exist

        await context.SaveChangesAsync();

        var pageModel = new IndexModel(context);

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        Assert.Equal(1, pageModel.TotalWhiskies);
        Assert.Equal(2, pageModel.TotalBottles);
        Assert.Equal(2, pageModel.TotalTags);
        Assert.Equal(1, pageModel.PendingTagsCount);
        // Orphan: Bottle #2 has CollectionId 999 but no actual Collection object in memory
        Assert.True(pageModel.OrphanedRecords >= 1);
    }
}
