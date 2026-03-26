using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace WhiskeyTracker.Web.Pages.Tasting;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IHubContext<TastingHub> _hubContext;

    public IndexModel(AppDbContext context, IHubContext<TastingHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public List<TastingSession> Sessions { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        Sessions = await _context.TastingSessions
            .Include(s => s.Notes)
            .Include(s => s.Participants)
            .Where(s => s.UserId == userId || s.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostJoinAsync(string joinCode)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Account/Login");

        var session = await _context.TastingSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.JoinCode == joinCode);

        if (session == null)
        {
            TempData["ErrorMessage"] = "Invalid Join Code.";
            return RedirectToPage();
        }

        if (session.UserId == userId || session.Participants.Any(p => p.UserId == userId))
        {
            return RedirectToPage("./Wizard", new { sessionId = session.Id });
        }

        _context.SessionParticipants.Add(new SessionParticipant
        {
            TastingSessionId = session.Id,
            UserId = userId,
            IsDriver = false,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        
        // Notify participants
        var userDisplayName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name ?? "A friend";
        await _hubContext.Clients.Group($"session_{session.Id}").SendAsync("ParticipantJoined", userDisplayName);

        return RedirectToPage("./Wizard", new { sessionId = session.Id });
    }
}
