using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Admin;

public class WhiskiesModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<WhiskiesModel> _logger;

    public WhiskiesModel(AppDbContext context, ILogger<WhiskiesModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<WhiskeyViewModel> Whiskies { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public const int PageSize = 50;

    public class WhiskeyViewModel
    {
        public int Id { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int BottleCount { get; set; }
    }

    public async Task OnGetAsync(int p = 1)
    {
        CurrentPage = p;

        var totalWhiskies = await _context.Whiskies.CountAsync();
        TotalPages = (int)Math.Ceiling(totalWhiskies / (double)PageSize);

        Whiskies = await _context.Whiskies
            .OrderBy(w => w.Brand)
            .ThenBy(w => w.Name)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(w => new WhiskeyViewModel
            {
                Id = w.Id,
                Brand = w.Brand,
                Name = w.Name,
                Type = w.Type,
                Region = w.Region,
                BottleCount = w.Bottles.Count
            })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteWhiskeyAsync(int whiskeyId)
    {
        var whiskey = await _context.Whiskies
            .Include(w => w.Bottles)
                .ThenInclude(b => b.TastingNotes)
            .FirstOrDefaultAsync(w => w.Id == whiskeyId);

        if (whiskey == null) return NotFound();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var bottle in whiskey.Bottles)
            {
                var blendComponents = await _context.BlendComponents
                    .Where(bc => bc.SourceBottleId == bottle.Id || bc.InfinityBottleId == bottle.Id)
                    .ToListAsync();
                _context.BlendComponents.RemoveRange(blendComponents);
                _context.TastingNotes.RemoveRange(bottle.TastingNotes);
            }

            // TastingNotes attached directly to the whiskey (not via a bottle)
            var directNotes = await _context.TastingNotes
                .Where(tn => tn.WhiskeyId == whiskeyId && tn.BottleId == null)
                .ToListAsync();
            _context.TastingNotes.RemoveRange(directNotes);

            _context.Bottles.RemoveRange(whiskey.Bottles);
            _context.Whiskies.Remove(whiskey);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Message"] = $"Whiskey \"{whiskey.Brand} {whiskey.Name}\" and all associated data have been deleted.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting whiskey {WhiskeyId}", whiskeyId);
            TempData["ErrorMessage"] = "A critical error occurred while deleting the whiskey. The operation was rolled back.";
        }

        return RedirectToPage();
    }
}
