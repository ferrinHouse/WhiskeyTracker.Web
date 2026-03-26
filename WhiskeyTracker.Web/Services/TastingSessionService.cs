using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Hubs;

namespace WhiskeyTracker.Web.Services;

public class TastingSessionService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<TastingHub> _hubContext;

    public TastingSessionService(AppDbContext context, IHubContext<TastingHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<(bool success, int? sessionId, string? error)> JoinSessionAsync(string joinCode, string userId, string? userDisplayName)
    {
        var session = await _context.TastingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

        if (session == null)
            return (false, null, "Invalid Join Code.");

        // If already in session, just return success
        if (session.UserId == userId || session.Participants.Any(p => p.UserId == userId))
            return (true, session.Id, null);

        _context.SessionParticipants.Add(new SessionParticipant
        {
            TastingSessionId = session.Id,
            UserId = userId,
            IsDriver = false,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        
        // Notify participants
        await _hubContext.Clients.Group($"session_{session.Id}").SendAsync("ParticipantJoined", userDisplayName ?? "A friend");

        return (true, session.Id, null);
    }
}
