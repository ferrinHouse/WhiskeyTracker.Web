using Microsoft.AspNetCore.SignalR;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Hubs;
using WhiskeyTracker.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WhiskeyTracker.Tests;

public class TastingSessionServiceTests : TestBase
{
    [Fact]
    public async Task JoinSessionAsync_ReturnsSuccess_WhenCodeIsValid()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var joinerId = "joiner";
        var session = new TastingSession { UserId = ownerId, JoinCode = "VALID_CODE" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);

        // ACT
        var (success, sessionId, error) = await service.JoinSessionAsync("VALID_CODE", joinerId, "New Joiner");

        // ASSERT
        Assert.True(success);
        Assert.Equal(session.Id, sessionId);
        Assert.Null(error);

        var participant = await context.SessionParticipants.FirstOrDefaultAsync(p => p.TastingSessionId == session.Id && p.UserId == joinerId);
        Assert.NotNull(participant);
    }

    [Fact]
    public async Task JoinSessionAsync_ReturnsFalse_WhenCodeIsInvalid()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);

        // ACT
        var (success, sessionId, error) = await service.JoinSessionAsync("WRONG", "user1", "Name");

        // ASSERT
        Assert.False(success);
        Assert.Null(sessionId);
        Assert.Equal("Invalid Join Code.", error);
    }

    [Fact]
    public async Task JoinSessionAsync_DoesNotAddDuplicate_WhenUserIsAlreadyParticipant()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var tasterId = "taster";
        var session = new TastingSession { UserId = ownerId, JoinCode = "CODE" };
        context.TastingSessions.Add(session);
        context.SessionParticipants.Add(new SessionParticipant { TastingSessionId = session.Id, UserId = tasterId });
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);

        // ACT
        var (success, sessionId, error) = await service.JoinSessionAsync("CODE", tasterId, "Taster");

        // ASSERT
        Assert.True(success);
        Assert.Equal(session.Id, sessionId);
        
        var participantCount = await context.SessionParticipants.CountAsync(p => p.TastingSessionId == session.Id && p.UserId == tasterId);
        Assert.Equal(1, participantCount);
    }

    [Fact]
    public async Task JoinSessionAsync_SendsSignalRNotification_OnSuccess()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var ownerId = "owner";
        var session = new TastingSession { UserId = ownerId, JoinCode = "NOTIFY_ME" };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var mockHubContext = new Mock<IHubContext<TastingHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group($"session_{session.Id}")).Returns(mockClientProxy.Object);

        var service = new TastingSessionService(context, mockHubContext.Object);

        // ACT
        await service.JoinSessionAsync("NOTIFY_ME", "joiner1", "New Friend");

        // ASSERT
        mockClientProxy.Verify(
            c => c.SendCoreAsync("ParticipantJoined", It.Is<object[]>(o => o.Contains("New Friend")), default),
            Times.Once
        );
    }
}
