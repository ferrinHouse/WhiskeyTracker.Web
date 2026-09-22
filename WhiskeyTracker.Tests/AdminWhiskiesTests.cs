using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Admin;
using Microsoft.Extensions.Logging.Abstractions;

namespace WhiskeyTracker.Tests;

public class AdminWhiskiesTests : TestBase
{
    private new AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task OnGet_ReturnsWhiskiesWithBottleCounts()
    {
        using var context = GetInMemoryContext();
        var whiskey1 = new Whiskey { Id = 1, Name = "Ardbeg", Brand = "Ardbeg", Type = "Single Malt", Region = "Islay" };
        var whiskey2 = new Whiskey { Id = 2, Name = "Macallan", Brand = "Macallan", Type = "Single Malt", Region = "Speyside" };
        context.Whiskies.AddRange(whiskey1, whiskey2);
        context.Bottles.Add(new Bottle { WhiskeyId = 1, CurrentVolumeMl = 700 });
        context.Bottles.Add(new Bottle { WhiskeyId = 1, CurrentVolumeMl = 700 });
        await context.SaveChangesAsync();

        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        await pageModel.OnGetAsync();

        Assert.Equal(2, pageModel.Whiskies.Count);
        var ardbeg = pageModel.Whiskies.First(w => w.Name == "Ardbeg");
        Assert.Equal(2, ardbeg.BottleCount);
        var macallan = pageModel.Whiskies.First(w => w.Name == "Macallan");
        Assert.Equal(0, macallan.BottleCount);
    }

    [Fact]
    public async Task OnGet_ReturnsPaginatedResults()
    {
        using var context = GetInMemoryContext();
        for (int i = 1; i <= 60; i++)
            context.Whiskies.Add(new Whiskey { Name = $"Whiskey{i:D2}", Brand = $"Brand{i:D2}" });
        await context.SaveChangesAsync();

        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        await pageModel.OnGetAsync(p: 1);

        Assert.Equal(WhiskiesModel.PageSize, pageModel.Whiskies.Count);
        Assert.Equal(2, pageModel.TotalPages);
    }

    [Fact]
    public async Task OnPostDeleteWhiskey_ReturnsNotFound_WhenWhiskeyMissing()
    {
        using var context = GetInMemoryContext();
        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        SetMockTempData(pageModel);

        var result = await pageModel.OnPostDeleteWhiskeyAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostDeleteWhiskey_DeletesWhiskey_WithNoBottles()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Solo", Brand = "Test" });
        await context.SaveChangesAsync();

        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        SetMockTempData(pageModel);

        var result = await pageModel.OnPostDeleteWhiskeyAsync(1);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(context.Whiskies);
    }

    [Fact]
    public async Task OnPostDeleteWhiskey_CascadesDelete_ThroughBottlesAndNotes()
    {
        using var context = GetInMemoryContext();
        var whiskey = new Whiskey { Id = 1, Name = "Cascade", Brand = "Test" };
        context.Whiskies.Add(whiskey);
        var bottle = new Bottle { Id = 10, WhiskeyId = 1, CurrentVolumeMl = 700 };
        context.Bottles.Add(bottle);
        context.TastingNotes.Add(new TastingNote { WhiskeyId = 1, BottleId = 10, Notes = "nice", Rating = 8 });
        context.TastingNotes.Add(new TastingNote { WhiskeyId = 1, BottleId = null, Notes = "direct", Rating = 7 });
        await context.SaveChangesAsync();

        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        SetMockTempData(pageModel);

        var result = await pageModel.OnPostDeleteWhiskeyAsync(1);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(context.Whiskies);
        Assert.Empty(context.Bottles);
        Assert.Empty(context.TastingNotes);
    }

    [Fact]
    public async Task OnPostDeleteWhiskey_RemovesBlendComponents()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Blended", Brand = "Test" });
        var bottle = new Bottle { Id = 10, WhiskeyId = 1, CurrentVolumeMl = 700 };
        context.Bottles.Add(bottle);
        context.BlendComponents.Add(new BlendComponent { SourceBottleId = 10, InfinityBottleId = 10, AmountAddedMl = 50 });
        await context.SaveChangesAsync();

        var pageModel = new WhiskiesModel(context, NullLogger<WhiskiesModel>.Instance);
        SetMockTempData(pageModel);

        var result = await pageModel.OnPostDeleteWhiskeyAsync(1);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(context.BlendComponents);
    }
}
