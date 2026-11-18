using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using Xunit;

namespace WhiskeyTracker.Tests;

public class GlobalRatingTests : TestBase
{
    [Fact]
    public async Task Details_GlobalAverage_AveragesAllUsersNotes_ButKeepsNotesPrivate()
    {
        using var context = GetInMemoryContext();
        var whiskey = new Whiskey { Name = "Ardbeg 10" };
        var session = new TastingSession { Title = "Night" };
        context.Whiskies.Add(whiskey);
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();
        context.TastingNotes.AddRange(
            new TastingNote { WhiskeyId = whiskey.Id, TastingSessionId = session.Id, UserId = "me", Rating = 5 },
            new TastingNote { WhiskeyId = whiskey.Id, TastingSessionId = session.Id, UserId = "other", Rating = 3 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear(); // mimic a fresh per-request context

        var model = new DetailsModel(context);
        SetMockUser(model, "me");
        await model.OnGetAsync(whiskey.Id);

        Assert.Equal(4.0, model.GlobalAverageRating);
        Assert.Equal(2, model.GlobalRatingCount);
        Assert.Single(model.Whiskey.TastingNotes); // only my own note is loaded
    }

    [Fact]
    public async Task Details_GlobalAverage_IsNull_WhenNoRatings()
    {
        using var context = GetInMemoryContext();
        var whiskey = new Whiskey { Name = "Unrated" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var model = new DetailsModel(context);
        SetMockUser(model, "me");
        await model.OnGetAsync(whiskey.Id);

        Assert.Null(model.GlobalAverageRating);
        Assert.Equal(0, model.GlobalRatingCount);
    }

    [Fact]
    public async Task Details_GlobalAverage_IgnoresOtherWhiskies()
    {
        using var context = GetInMemoryContext();
        var a = new Whiskey { Name = "A" };
        var b = new Whiskey { Name = "B" };
        context.Whiskies.AddRange(a, b);
        await context.SaveChangesAsync();
        context.TastingNotes.AddRange(
            new TastingNote { WhiskeyId = a.Id, UserId = "u1", Rating = 2 },
            new TastingNote { WhiskeyId = b.Id, UserId = "u1", Rating = 5 });
        await context.SaveChangesAsync();

        var model = new DetailsModel(context);
        SetMockUser(model, "me");
        await model.OnGetAsync(a.Id);

        Assert.Equal(2.0, model.GlobalAverageRating);
        Assert.Equal(1, model.GlobalRatingCount);
    }
}
