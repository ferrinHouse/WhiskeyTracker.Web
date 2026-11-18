using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;

    public DetailsModel(AppDbContext context)
    {
        _context = context;
    }

    public Whiskey Whiskey { get; set; } = default!;

    /// <summary>Minimum ratings before the global average is shown (prevents deducing another user's rating).</summary>
    public const int MinGlobalRatings = 3;

    /// <summary>Average rating across every user's tasting notes, or null until there are <see cref="MinGlobalRatings"/> ratings.</summary>
    public double? GlobalAverageRating { get; set; }

    /// <summary>Number of tasting notes (across all users) that make up the global average.</summary>
    public int GlobalRatingCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // fetch the whiskey AND its bottles in one go
        // Logic: Show bottles that belong to a collection I am a member of.
        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        var whiskey = await _context.Whiskies
            .Include(w => w.Bottles.Where(b => b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value)))
              .ThenInclude(b => b.Collection)
            .Include(w => w.Bottles.Where(b => b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value)))
              .ThenInclude(b => b.Purchaser)

            .Include(tn => tn.TastingNotes.Where(n => n.UserId == userId)) // <--- Keep Notes Private for now
              .ThenInclude(ts => ts.TastingSession) 
            .Include(tn => tn.TastingNotes.Where(n => n.UserId == userId))
              .ThenInclude(nt => nt.TastingNoteTags)
              .ThenInclude(tnt => tnt.Tag)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (whiskey == null) return NotFound();

        // Aggregate only (no note content), so other users' notes stay private.
        var stats = await _context.TastingNotes
            .Where(n => n.WhiskeyId == whiskey.Id)
            .GroupBy(n => n.WhiskeyId)
            .Select(g => new { Count = g.Count(), Average = g.Average(n => (double)n.Rating) })
            .FirstOrDefaultAsync();

        GlobalRatingCount = stats?.Count ?? 0;
        GlobalAverageRating = stats != null && stats.Count >= MinGlobalRatings ? stats.Average : null;

        Whiskey = whiskey;
        return Page();
    }
}