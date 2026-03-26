using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using System.Security.Claims;

namespace WhiskeyTracker.Web.Pages.Tasting;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;

    public DetailsModel(AppDbContext context)
    {
        _context = context;
    }

    public TastingSession Session { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return NotFound();

        var session = await _context.TastingSessions
            .Include(s => s.Notes)
                .ThenInclude(n => n.Whiskey)
            .Include(s => s.Notes)
                .ThenInclude(n => n.Bottle)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (session == null)
        {
            return NotFound();
        }

        Session = session;
        return Page();
    }
}
