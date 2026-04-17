using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Admin;
using Xunit;

namespace WhiskeyTracker.Tests;

public class AdminWhiskiesTests : TestBase
{
    [Fact]
    public async Task MergeWhiskies_MovesRelatedData()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<WhiskiesModel>>();
        var pageModel = new WhiskiesModel(context, logger.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        pageModel.TempData = tempData;

        var sourceWhiskey = new Whiskey { Brand = "Source", Name = "To Merge", Distillery = "D1" };
        var targetWhiskey = new Whiskey { Brand = "Target", Name = "To Keep", Distillery = "D1" };
        context.Whiskies.AddRange(sourceWhiskey, targetWhiskey);
        await context.SaveChangesAsync();

        var bottle = new Bottle { WhiskeyId = sourceWhiskey.Id, CurrentVolumeMl = 750, CapacityMl = 750 };
        context.Bottles.Add(bottle);

        var tastingSession = new TastingSession { Title = "Session", Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        context.TastingSessions.Add(tastingSession);
        await context.SaveChangesAsync();

        var note = new TastingNote { WhiskeyId = sourceWhiskey.Id, TastingSessionId = tastingSession.Id, Rating = 5 };
        context.TastingNotes.Add(note);

        var lineupItem = new SessionLineupItem { WhiskeyId = sourceWhiskey.Id, TastingSessionId = tastingSession.Id, OrderIndex = 1 };
        context.SessionLineupItems.Add(lineupItem);
        await context.SaveChangesAsync();

        // Act
        var result = await pageModel.OnPostMergeWhiskeyAsync(sourceWhiskey.Id, targetWhiskey.Id);

        // Assert
        Assert.IsType<RedirectToPageResult>(result);

        var movedBottle = await context.Bottles.FirstOrDefaultAsync(b => b.Id == bottle.Id);
        Assert.NotNull(movedBottle);
        Assert.Equal(targetWhiskey.Id, movedBottle.WhiskeyId);

        var movedNote = await context.TastingNotes.FirstOrDefaultAsync(n => n.Id == note.Id);
        Assert.NotNull(movedNote);
        Assert.Equal(targetWhiskey.Id, movedNote.WhiskeyId);

        var movedLineupItem = await context.SessionLineupItems.FirstOrDefaultAsync(sli => sli.Id == lineupItem.Id);
        Assert.NotNull(movedLineupItem);
        Assert.Equal(targetWhiskey.Id, movedLineupItem.WhiskeyId);

        var deletedWhiskey = await context.Whiskies.FindAsync(sourceWhiskey.Id);
        Assert.Null(deletedWhiskey);
    }

    [Fact]
    public async Task DeleteWhiskey_DeletesWhenNoDependencies()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<WhiskiesModel>>();
        var pageModel = new WhiskiesModel(context, logger.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        pageModel.TempData = tempData;

        var whiskey = new Whiskey { Brand = "To Delete", Name = "Empty", Distillery = "D1" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        // Act
        var result = await pageModel.OnPostDeleteWhiskeyAsync(whiskey.Id);

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var deletedWhiskey = await context.Whiskies.FindAsync(whiskey.Id);
        Assert.Null(deletedWhiskey);
    }

    [Fact]
    public async Task DeleteWhiskey_FailsWhenHasBottles()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<WhiskiesModel>>();
        var pageModel = new WhiskiesModel(context, logger.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        pageModel.TempData = tempData;

        var whiskey = new Whiskey { Brand = "To Delete", Name = "With Bottle", Distillery = "D1" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var bottle = new Bottle { WhiskeyId = whiskey.Id, CurrentVolumeMl = 750, CapacityMl = 750 };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        // Act
        var result = await pageModel.OnPostDeleteWhiskeyAsync(whiskey.Id);

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var notDeletedWhiskey = await context.Whiskies.FindAsync(whiskey.Id);
        Assert.NotNull(notDeletedWhiskey);
        Assert.True(tempData.ContainsKey("ErrorMessage"));
    }
}
