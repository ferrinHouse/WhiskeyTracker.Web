using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;


    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public int TotalWhiskies { get; set; }
    public int OpenBottles { get; set; }
    public int TotalSessions { get; set; }

    public List<TastingNote> RecentNotes { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        var myCollectionIds = await _context.CollectionMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.CollectionId)
            .ToListAsync();

        TotalWhiskies = await _context.Bottles
            .Where(b => b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
            .Select(b => b.WhiskeyId)
            .Distinct()
            .CountAsync();

        OpenBottles = await _context.Bottles
            .CountAsync(b => b.Status == BottleStatus.Opened && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));

        TotalSessions = await _context.TastingSessions
            .CountAsync(s => s.UserId == userId);

        RecentNotes = await _context.TastingNotes
            .Include(n => n.Whiskey)
            .Where(n => n.Bottle != null && n.Bottle.CollectionId.HasValue && myCollectionIds.Contains(n.Bottle.CollectionId.Value))
            .OrderByDescending(n => n.Id)
            .Take(5)
            .ToListAsync();
    }
}
