using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        return RedirectToPage("./Wizard", new { sessionId = Session.Id });
    }
}