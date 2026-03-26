using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public int TotalUsers { get; set; }
    public int TotalWhiskies { get; set; }
    public int TotalBottles { get; set; }
    public int TotalCollections { get; set; }
    public int TotalTastingNotes { get; set; }
    public int TotalTags { get; set; }
    public int PendingTagsCount { get; set; }
    public int OrphanedRecords { get; set; }

    public async Task OnGetAsync()
    {
        TotalUsers = await _context.Users.CountAsync();
        TotalWhiskies = await _context.Whiskies.CountAsync();
        TotalBottles = await _context.Bottles.CountAsync();
        TotalCollections = await _context.Collections.CountAsync();
        TotalTastingNotes = await _context.TastingNotes.CountAsync();
        TotalTags = await _context.Tags.CountAsync();
        PendingTagsCount = await _context.Tags.CountAsync(t => !t.IsApproved);

        // Count orphaned records
        var orphanedBottles = await _context.Bottles
            .CountAsync(b => (b.CollectionId.HasValue && b.Collection == null) || (b.UserId != null && b.Purchaser == null));
        
        var orphanedMembers = await _context.CollectionMembers
            .CountAsync(m => m.User == null || m.Collection == null);

        var orphanedNotes = await _context.TastingNotes
            .CountAsync(n => n.BottleId.HasValue && n.Bottle == null);

        var orphanedBlends = await _context.BlendComponents
            .CountAsync(bc => bc.SourceBottle == null || bc.InfinityBottle == null);

        OrphanedRecords = orphanedBottles + orphanedMembers + orphanedNotes + orphanedBlends;
    }
}
