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
            query = query.Where(w => w.Brand.Contains(SearchString)
                                || w.Name.Contains(SearchString)
                                || w.Distillery.Contains(SearchString));
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
        var whiskey = await _context.Whiskies.FindAsync(whiskeyId);
        if (whiskey == null) return NotFound();

        var hasBottles = await _context.Bottles.AnyAsync(b => b.WhiskeyId == whiskeyId);
        var hasNotes = await _context.TastingNotes.AnyAsync(n => n.WhiskeyId == whiskeyId);
        var hasLineupItems = await _context.SessionLineupItems.AnyAsync(sli => sli.WhiskeyId == whiskeyId);

        if (hasBottles || hasNotes || hasLineupItems)
        {
            TempData["ErrorMessage"] = "Cannot delete whiskey that has bottles, tasting notes, or session lineup entries. Consider merging it instead.";
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

        var sourceWhiskey = await _context.Whiskies.FindAsync(sourceId);
        var targetWhiskey = await _context.Whiskies.FindAsync(targetId);

        if (sourceWhiskey == null || targetWhiskey == null)
        {
            TempData["ErrorMessage"] = "Source or target whiskey not found.";
            return RedirectToPage();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Move Bottles
            await _context.Bottles
                .Where(b => b.WhiskeyId == sourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.WhiskeyId, targetId));

            // Move Tasting Notes
            await _context.TastingNotes
                .Where(n => n.WhiskeyId == sourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.WhiskeyId, targetId));

            // Move SessionLineupItems
            await _context.SessionLineupItems
                .Where(sli => sli.WhiskeyId == sourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(sli => sli.WhiskeyId, targetId));

            _context.Whiskies.Remove(sourceWhiskey);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            TempData["Message"] = $"Successfully merged '{sourceWhiskey.Brand} {sourceWhiskey.Name}' into '{targetWhiskey.Brand} {targetWhiskey.Name}'.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error merging whiskey {SourceId} into {TargetId}", sourceId, targetId);
            TempData["ErrorMessage"] = "An error occurred during merging.";
        }

        return RedirectToPage();
    }
}
