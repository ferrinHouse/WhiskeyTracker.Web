using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Tasting;
using System.Security.Claims;
using Xunit;

namespace WhiskeyTracker.Tests;

public class SessionManagementTests
{
    private AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private void SetMockUser(PageModel page, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        page.PageContext = new PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task Index_OnGet_ReturnsOnlyUsersSessions()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "user1";
        var otherUserId = "user2";

        context.TastingSessions.AddRange(
            new TastingSession { UserId = userId, Title = "User 1 Session 1", Date = new DateOnly(2026, 1, 1) },
            new TastingSession { UserId = userId, Title = "User 1 Session 2", Date = new DateOnly(2026, 1, 2) },
            new TastingSession { UserId = otherUserId, Title = "Other User Session", Date = new DateOnly(2026, 1, 1) }
        );
        await context.SaveChangesAsync();

        var pageModel = new WhiskeyTracker.Web.Pages.Tasting.IndexModel(context);
        SetMockUser(pageModel, userId);

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        Assert.Equal(2, pageModel.Sessions.Count);
        Assert.All(pageModel.Sessions, s => Assert.Equal(userId, s.UserId));
        Assert.Equal("User 1 Session 2", pageModel.Sessions[0].Title); // Ordered by date desc
    }

    [Fact]
    public async Task Details_OnGet_ReturnsNotFound_IfSessionNotExists()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var pageModel = new WhiskeyTracker.Web.Pages.Tasting.DetailsModel(context);
        SetMockUser(pageModel, "user1");

        // ACT
        var result = await pageModel.OnGetAsync(999);

        // ASSERT
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_OnGet_ReturnsNotFound_IfSessionBelongsToOtherUser()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var otherUserId = "user2";
        var session = new TastingSession { UserId = otherUserId, Title = "Other User Session", Date = new DateOnly(2026, 1, 1) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var pageModel = new WhiskeyTracker.Web.Pages.Tasting.DetailsModel(context);
        SetMockUser(pageModel, "user1");

        // ACT
        var result = await pageModel.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_OnGet_ReturnsPage_WithCorrectData()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "user1";
        var whiskey = new Whiskey { Name = "Test Whiskey", Distillery = "Test Distillery" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var session = new TastingSession { UserId = userId, Title = "My Session", Date = new DateOnly(2026, 1, 1) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        context.TastingNotes.Add(new TastingNote 
        { 
            TastingSessionId = session.Id, 
            WhiskeyId = whiskey.Id, 
            Rating = 5, 
            Notes = "Excellent", 
            OrderIndex = 1 
        });
        await context.SaveChangesAsync();

        var pageModel = new WhiskeyTracker.Web.Pages.Tasting.DetailsModel(context);
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<PageResult>(result);
        Assert.Equal("My Session", pageModel.Session.Title);
        Assert.Single(pageModel.Session.Notes);
        Assert.Equal("Test Whiskey", pageModel.Session.Notes[0].Whiskey.Name);
    }
}
