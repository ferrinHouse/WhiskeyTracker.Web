using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Tasting;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Hubs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WhiskeyTracker.Tests;

public class CollaborativeSessionTests : TestBase
{
    private void SetMockTempData(PageModel page)
    {
        var mockTempData = new Mock<ITempDataDictionary>();
        page.TempData = mockTempData.Object;
    }

    private Mock<IHubContext<TastingHub>> GetMockHubContext()
    {
        var mockHubContext = new Mock<IHubContext<TastingHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        
        return mockHubContext;
    }

    [Fact]
    public async Task WizardModel_OnGet_AllowsOwner()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var session = new TastingSession { UserId = ownerId, Title = "Owner Session" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var model = new WizardModel(context, hubMock.Object);
        SetMockUser(model, ownerId);

        // ACT
        var result = await model.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<PageResult>(result);
        Assert.Equal(session.Id, model.Session.Id);
    }

    [Fact]
    public async Task WizardModel_OnGet_AllowsParticipant()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var tasterId = "taster";
        var session = new TastingSession { UserId = ownerId, Title = "Shared Session" };
        context.TastingSessions.Add(session);
        context.SessionParticipants.Add(new SessionParticipant { TastingSessionId = session.Id, UserId = tasterId });
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var model = new WizardModel(context, hubMock.Object);
        SetMockUser(model, tasterId);

        // ACT
        var result = await model.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<PageResult>(result);
        Assert.Equal(session.Id, model.Session.Id);
    }

    [Fact]
    public async Task WizardModel_OnGet_ReturnsNotFound_ForUnauthorizedUser()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var strangerId = "stranger";
        var session = new TastingSession { UserId = ownerId, Title = "Private Session" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var model = new WizardModel(context, hubMock.Object);
        SetMockUser(model, strangerId);

        // ACT
        var result = await model.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task IndexModel_OnPostJoin_AddsParticipant_WithValidCode()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var joinerId = "joiner";
        var session = new TastingSession { UserId = ownerId, JoinCode = "JOINME" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var model = new IndexModel(context, hubMock.Object);
        SetMockUser(model, joinerId);
        SetMockTempData(model);

        // ACT
        var result = await model.OnPostJoinAsync("JOINME");

        // ASSERT
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./Wizard", redirect.PageName);
        
        var participant = await context.SessionParticipants.FirstOrDefaultAsync(p => p.TastingSessionId == session.Id && p.UserId == joinerId);
        Assert.NotNull(participant);
    }

    [Fact]
    public async Task DetailsModel_OnGet_AllowsParticipant()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var tasterId = "taster";
        var session = new TastingSession { UserId = ownerId, Title = "Shared Session" };
        context.TastingSessions.Add(session);
        context.SessionParticipants.Add(new SessionParticipant { TastingSessionId = session.Id, UserId = tasterId });
        await context.SaveChangesAsync();

        var model = new DetailsModel(context);
        SetMockUser(model, tasterId);

        // ACT
        var result = await model.OnGetAsync(session.Id);

        // ASSERT
        Assert.IsType<PageResult>(result);
        Assert.Equal(session.Id, model.Session.Id);
    }

    [Fact]
    public async Task WizardModel_OnPost_UpdatesSharedLineup()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var whiskey = new Whiskey { Name = "Test Whiskey", Distillery = "Distillery" };
        context.Whiskies.Add(whiskey);
        var session = new TastingSession { UserId = ownerId, Title = "Shared Session" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var model = new WizardModel(context, hubMock.Object)
        {
            SelectedWhiskeyId = whiskey.Id,
            PourAmountOz = 1.0,
            NewNote = new TastingNote { Rating = 4, Notes = "Added by owner" }
        };
        SetMockUser(model, ownerId);
        SetMockTempData(model);

        // ACT
        await model.OnPostAsync(session.Id);

        // ASSERT
        var lineupItem = await context.SessionLineupItems.FirstOrDefaultAsync(l => l.TastingSessionId == session.Id && l.WhiskeyId == whiskey.Id);
        Assert.NotNull(lineupItem);
    }
}
