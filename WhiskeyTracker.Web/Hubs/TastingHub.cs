using Microsoft.AspNetCore.SignalR;

namespace WhiskeyTracker.Web.Hubs;

public class TastingHub : Hub
{
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
    }

    public async Task NotifyWhiskeyAdded(string sessionId, string whiskeyName, string userName)
    {
        await Clients.Group($"session_{sessionId}").SendAsync("WhiskeyAdded", whiskeyName, userName);
    }

    public async Task NotifyCurrentWhiskeyChanged(string sessionId, int lineupIndex)
    {
        await Clients.Group($"session_{sessionId}").SendAsync("CurrentWhiskeyChanged", lineupIndex);
    }

    public async Task NotifyParticipantJoined(string sessionId, string userName)
    {
        await Clients.Group($"session_{sessionId}").SendAsync("ParticipantJoined", userName);
    }

    public async Task NotifyNoteUpdated(string sessionId, string userName)
    {
        await Clients.Group($"session_{sessionId}").SendAsync("NoteUpdated", userName);
    }
}
