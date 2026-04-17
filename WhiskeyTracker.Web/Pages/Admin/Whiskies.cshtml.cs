using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class WhiskiesModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<WhiskiesModel> _logger;

    public WhiskiesModel(AppDbContext context, ILogger<WhiskiesModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<Whiskey> Whiskies { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public const int PageSize = 50;

    public async Task OnGetAsync(int p = 1)
    {
        CurrentPage = p;

        IQueryable<Whiskey> query = _context.Whiskies.OrderBy(w => w.Brand).ThenBy(w => w.Name);

        if (!string.IsNullOrEmpty(SearchString))
        {
            var lowerSearch = SearchString.ToLower();
            query = query.Where(w => w.Brand.ToLower().Contains(lowerSearch)
                                || w.Name.ToLower().Contains(lowerSearch)
                                || w.Distillery.ToLower().Contains(lowerSearch));
        }

        var totalWhiskies = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalWhiskies / (double)PageSize);

        Whiskies = await query
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteWhiskeyAsync(int whiskeyId)
    {
        var whiskey = await _context.Whiskies
            .Include(w => w.Bottles)
            .Include(w => w.TastingNotes)
            .FirstOrDefaultAsync(w => w.Id == whiskeyId);

        if (whiskey == null) return NotFound();

        if (whiskey.Bottles.Any() || whiskey.TastingNotes.Any())
        {
            TempData["ErrorMessage"] = "Cannot delete whiskey that has bottles or tasting notes. Consider merging it instead.";
            return RedirectToPage();
        }

        _context.Whiskies.Remove(whiskey);
        await _context.SaveChangesAsync();

        TempData["Message"] = $"Whiskey '{whiskey.Brand} {whiskey.Name}' deleted successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMergeWhiskeyAsync(int sourceId, int targetId)
    {
        if (sourceId == targetId)
        {
            TempData["ErrorMessage"] = "Source and target whiskey cannot be the same.";
            return RedirectToPage();
        }

        var sourceWhiskey = await _context.Whiskies
            .Include(w => w.Bottles)
            .Include(w => w.TastingNotes)
            .FirstOrDefaultAsync(w => w.Id == sourceId);

        var targetWhiskey = await _context.Whiskies.FindAsync(targetId);

        if (sourceWhiskey == null || targetWhiskey == null)
        {
            TempData["ErrorMessage"] = "Source or target whiskey not found.";
            return RedirectToPage();
        }

        try
        {
            // Move Bottles
            foreach (var bottle in sourceWhiskey.Bottles)
            {
                bottle.WhiskeyId = targetId;
            }

            // Move Tasting Notes
            foreach (var note in sourceWhiskey.TastingNotes)
            {
                note.WhiskeyId = targetId;
            }

            // Move SessionLineupItems
            var lineupItems = await _context.SessionLineupItems
                .Where(sli => sli.WhiskeyId == sourceId)
                .ToListAsync();
            foreach (var item in lineupItems)
            {
                item.WhiskeyId = targetId;
            }

            _context.Whiskies.Remove(sourceWhiskey);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Successfully merged '{sourceWhiskey.Brand} {sourceWhiskey.Name}' into '{targetWhiskey.Brand} {targetWhiskey.Name}'.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging whiskey {SourceId} into {TargetId}", sourceId, targetId);
            TempData["ErrorMessage"] = "An error occurred during merging.";
        }

        return RedirectToPage();
    }
}
