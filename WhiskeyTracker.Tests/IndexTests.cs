using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages;
using Xunit;

namespace WhiskeyTracker.Tests;

public class IndexTests : TestBase
{
    [Fact]
    public async Task OnGetAsync_FiltersDataByUserCollections()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var model = new IndexModel(context);
        
        var userA = "user-a";
        var userB = "user-b";
        
        // Setup User A's Collection and Bottle
        var collA = new Collection { Name = "User A Coll" };
        context.Collections.Add(collA);
        context.CollectionMembers.Add(new CollectionMember { UserId = userA, Collection = collA, Role = CollectionRole.Owner });
        
        var whiskeyA = new Whiskey { Name = "Whiskey A", Distillery = "Dist A" };
        var bottleA = new Bottle { Whiskey = whiskeyA, Collection = collA, Status = BottleStatus.Opened };
        context.Bottles.Add(bottleA);
        
        var sessionA = new TastingSession { UserId = userA, Title = "Session A", Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(sessionA);
        context.TastingNotes.Add(new TastingNote { Whiskey = whiskeyA, Bottle = bottleA, TastingSession = sessionA, UserId = userA, Notes = "Note A", Rating = 5 });
        
        // Setup User B's Data (Should be filtered out for User A)
        var collB = new Collection { Name = "User B Coll" };
        context.Collections.Add(collB);
        context.CollectionMembers.Add(new CollectionMember { UserId = userB, Collection = collB, Role = CollectionRole.Owner });
        
        var whiskeyB = new Whiskey { Name = "Whiskey B", Distillery = "Dist B" };
        var bottleB = new Bottle { Whiskey = whiskeyB, Collection = collB, Status = BottleStatus.Opened };
        context.Bottles.Add(bottleB);
        
        var sessionB = new TastingSession { UserId = userB, Title = "Session B", Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(sessionB);
        context.TastingNotes.Add(new TastingNote { Whiskey = whiskeyB, Bottle = bottleB, TastingSession = sessionB, UserId = userB, Notes = "Note B", Rating = 4 });
        
        await context.SaveChangesAsync();
        
        SetMockUser(model, userA);

        // Act
        await model.OnGetAsync();

        // Assert
        Assert.Equal(1, model.TotalWhiskies); // Only Whiskey A
        Assert.Equal(1, model.OpenBottles);   // Only Bottle A
        Assert.Equal(1, model.TotalSessions);  // Only Session A
        Assert.Single(model.RecentNotes);
        Assert.Equal("Whiskey A", model.RecentNotes[0].Whiskey.Name);
    }

    [Fact]
    public async Task OnGetAsync_ShowsSharedCollectionData()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var model = new IndexModel(context);
        
        var ownerId = "owner";
        var viewerId = "viewer";
        
        var sharedColl = new Collection { Name = "Shared Coll" };
        context.Collections.Add(sharedColl);
        context.CollectionMembers.Add(new CollectionMember { UserId = ownerId, Collection = sharedColl, Role = CollectionRole.Owner });
        context.CollectionMembers.Add(new CollectionMember { UserId = viewerId, Collection = sharedColl, Role = CollectionRole.Viewer });
        
        var whiskey = new Whiskey { Name = "Shared Whiskey", Distillery = "Shared Dist" };
        var bottle = new Bottle { Whiskey = whiskey, Collection = sharedColl, Status = BottleStatus.Opened };
        context.Bottles.Add(bottle);
        
        var session = new TastingSession { UserId = ownerId, Title = "Owner Session", Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        context.TastingNotes.Add(new TastingNote { Whiskey = whiskey, Bottle = bottle, TastingSession = session, UserId = ownerId, Notes = "Owner Note", Rating = 5 });
        
        await context.SaveChangesAsync();
        
        SetMockUser(model, viewerId);

        // Act
        await model.OnGetAsync();

        // Assert
        Assert.Equal(1, model.TotalWhiskies);
        Assert.Equal(1, model.OpenBottles);
        Assert.Equal(0, model.TotalSessions); // Sessions are user-specific (UserId == viewerId)
        Assert.Single(model.RecentNotes);     // Recent notes are collection-specific (Bottle in viewer's collection)
        Assert.Equal("Owner Note", model.RecentNotes[0].Notes);
    }
}
