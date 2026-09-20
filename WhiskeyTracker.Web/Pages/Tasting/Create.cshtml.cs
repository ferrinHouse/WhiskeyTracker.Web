using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Tasting;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CreateModel(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    [BindProperty]
    public TastingSession Session { get; set; } = default!;

    /// The whiskey the user picked "Taste" for, carried through from the library so it can be
    /// preselected once the session's Wizard page loads.
    [BindProperty(SupportsGet = true)]
    public int? WhiskeyId { get; set; }

    public void OnGet()
    {
        var now = _timeProvider.GetLocalNow();
        Session = new TastingSession
        {
            Date = DateOnly.FromDateTime(now.Date),
            Title = $"Tasting on {now:MMM dd, yyyy}"
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        Session.UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Session.JoinCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

        _context.TastingSessions.Add(Session);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Wizard", new { sessionId = Session.Id, whiskeyId = WhiskeyId });
    }

    /// Skips the "new session" form entirely: spins up a lightweight session behind the
    /// scenes for a quick, single-pour tasting note and drops the user straight into the Wizard.
    public async Task<IActionResult> OnPostSingleAsync(int whiskeyId)
    {
        var whiskeyExists = await _context.Whiskies.AnyAsync(w => w.Id == whiskeyId);
        if (!whiskeyExists) return NotFound();

        var now = _timeProvider.GetLocalNow();
        var session = new TastingSession
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Date = DateOnly.FromDateTime(now.Date),
            Title = $"Quick Taste on {now:MMM dd, yyyy}",
            JoinCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()
        };

        _context.TastingSessions.Add(session);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Wizard", new { sessionId = session.Id, whiskeyId });
    }
}