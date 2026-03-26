using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly WhiskeyTracker.Web.Services.LegacyMigrationService _legacyMigrationService;

    public IndexModel(AppDbContext context, WhiskeyTracker.Web.Services.LegacyMigrationService legacyMigrationService)
    {
        _context = context;
        _legacyMigrationService = legacyMigrationService;
    }

    // This list will hold the data we fetch so the HTML can see it
    public IList<Whiskey> Whiskies { get; set; } = default!;

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }
    public SelectList? Regions { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? WhiskeyRegion { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowOnlyMyCollection { get; set; }

    [BindProperty(SupportsGet = true)]
    public BottleStatus? Status { get; set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // --- 1. Runtime Migration: Ensure User has a Collection ---
        if (!string.IsNullOrEmpty(userId))
        {
            await _legacyMigrationService.EnsureUserHasCollectionAsync(userId);
        }
        
        IQueryable<string> genreQuery = _context.Whiskies
                                        .OrderBy(w => w.Region)
                                        .Select(w => w.Region)
                                        .Distinct();
        Regions = new SelectList(await genreQuery.ToListAsync());

        // Show Whiskies that exist in the DB.
        // Option: Filter to only show whiskies I have? No, library mode usually shows all reference data.
        IQueryable<Whiskey> whiskies = from w in _context.Whiskies
                                       orderby w.Distillery, w.Name
                                       select w;

        if (!string.IsNullOrEmpty(SearchString))
        {
            whiskies = whiskies.Where(s => s.Name.ToLower().Contains(SearchString.ToLower())
                                        || s.Distillery.ToLower().Contains(SearchString.ToLower()));
        }

        if (!string.IsNullOrEmpty(WhiskeyRegion))
        {
            whiskies = whiskies.Where(x => x.Region == WhiskeyRegion);
        }

        if (ShowOnlyMyCollection || Status.HasValue)
        {
            var myCollectionIds = await _context.CollectionMembers
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.CollectionId)
                .ToListAsync();

            var ownedWhiskeyIdsQuery = _context.Bottles
                .Where(b => b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
                .AsQueryable();

            if (Status.HasValue)
            {
                ownedWhiskeyIdsQuery = ownedWhiskeyIdsQuery.Where(b => b.Status == Status.Value);
            }

            var ownedWhiskeyIds = await ownedWhiskeyIdsQuery.Select(b => b.WhiskeyId).Distinct().ToListAsync();
            whiskies = whiskies.Where(w => ownedWhiskeyIds.Contains(w.Id));
        }

        Whiskies = await whiskies.ToListAsync();
    }
}