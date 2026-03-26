using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Web.Pages.Tasting;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly TastingSessionService _sessionService;

    public IndexModel(AppDbContext context, TastingSessionService sessionService)
    {
        _context = context;
        _sessionService = sessionService;
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

        var userDisplayName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name;
        
        var (success, sessionId, error) = await _sessionService.JoinSessionAsync(joinCode, userId, userDisplayName);

        if (!success)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToPage();
        }

        return RedirectToPage("./Wizard", new { sessionId });
    }
}
