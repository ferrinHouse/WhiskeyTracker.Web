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

    public SelectList? Types { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? WhiskeyType { get; set; }

    public SelectList? Brands { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? WhiskeyBrand { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<BottleStatus> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool ShowOnlyMyCollection { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortOrder { get; set; }

    public string BrandSort { get; set; } = string.Empty;
    public string NameSort { get; set; } = string.Empty;
    public string TypeSort { get; set; } = string.Empty;
    public string RegionSort { get; set; } = string.Empty;

    public bool HasFilterActive()
    {
        return !string.IsNullOrEmpty(SearchString) || 
               !string.IsNullOrEmpty(WhiskeyRegion) || 
               !string.IsNullOrEmpty(WhiskeyType) || 
               !string.IsNullOrEmpty(WhiskeyBrand) || 
               Statuses.Any() || 
               ShowOnlyMyCollection;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // --- 1. Runtime Migration: Ensure User has a Collection ---
        if (!string.IsNullOrEmpty(userId))
        {
            await _legacyMigrationService.EnsureUserHasCollectionAsync(userId);
        }

        BrandSort = string.IsNullOrEmpty(SortOrder) || SortOrder == "Brand" ? "brand_desc" : "Brand";
        NameSort = SortOrder == "Name" ? "name_desc" : "Name";
        TypeSort = SortOrder == "Type" ? "type_desc" : "Type";
        RegionSort = SortOrder == "Region" ? "region_desc" : "Region";
        
        IQueryable<string> genreQuery = _context.Whiskies
                                        .OrderBy(w => w.Region)
                                        .Select(w => w.Region)
                                        .Distinct();
        Regions = new SelectList(await genreQuery.ToListAsync());

        IQueryable<string> typeQuery = _context.Whiskies
                                        .Where(w => !string.IsNullOrEmpty(w.Type))
                                        .OrderBy(w => w.Type)
                                        .Select(w => w.Type)
                                        .Distinct();
        Types = new SelectList(await typeQuery.ToListAsync());

        IQueryable<string> brandQuery = _context.Whiskies
                                        .Where(w => !string.IsNullOrEmpty(w.Brand))
                                        .OrderBy(w => w.Brand)
                                        .Select(w => w.Brand)
                                        .Distinct();
        Brands = new SelectList(await brandQuery.ToListAsync());

        // Show Whiskies that exist in the DB.
        // Option: Filter to only show whiskies I have? No, library mode usually shows all reference data.
        IQueryable<Whiskey> whiskies = from w in _context.Whiskies
                                       orderby w.Brand, w.Name
                                       select w;

        if (!string.IsNullOrEmpty(SearchString))
        {
            whiskies = whiskies.Where(s => s.Name.ToLower().Contains(SearchString.ToLower())
                                        || s.Brand.ToLower().Contains(SearchString.ToLower())
                                        || s.Distillery.ToLower().Contains(SearchString.ToLower()));
        }

        if (!string.IsNullOrEmpty(WhiskeyRegion))
        {
            whiskies = whiskies.Where(x => x.Region == WhiskeyRegion);
        }

        if (!string.IsNullOrEmpty(WhiskeyType))
        {
            whiskies = whiskies.Where(x => x.Type == WhiskeyType);
        }

        if (!string.IsNullOrEmpty(WhiskeyBrand))
        {
            whiskies = whiskies.Where(x => x.Brand == WhiskeyBrand);
        }

        if (ShowOnlyMyCollection || Statuses.Any())
        {
            var myCollectionIds = await _context.CollectionMembers
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.CollectionId)
                .ToListAsync();

            var ownedWhiskeyIdsQuery = _context.Bottles
                .Where(b => b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
                .AsQueryable();

            if (Statuses.Any())
            {
                ownedWhiskeyIdsQuery = ownedWhiskeyIdsQuery.Where(b => Statuses.Contains(b.Status));
            }

            var ownedWhiskeyIds = await ownedWhiskeyIdsQuery.Select(b => b.WhiskeyId).Distinct().ToListAsync();
            whiskies = whiskies.Where(w => ownedWhiskeyIds.Contains(w.Id));
        }

        whiskies = SortOrder switch
        {
            "brand_desc" => whiskies.OrderByDescending(w => w.Brand).ThenBy(w => w.Name),
            "Name" => whiskies.OrderBy(w => w.Name).ThenBy(w => w.Brand),
            "name_desc" => whiskies.OrderByDescending(w => w.Name).ThenBy(w => w.Brand),
            "Type" => whiskies.OrderBy(w => w.Type).ThenBy(w => w.Brand).ThenBy(w => w.Name),
            "type_desc" => whiskies.OrderByDescending(w => w.Type).ThenBy(w => w.Brand).ThenBy(w => w.Name),
            "Region" => whiskies.OrderBy(w => w.Region).ThenBy(w => w.Brand).ThenBy(w => w.Name),
            "region_desc" => whiskies.OrderByDescending(w => w.Region).ThenBy(w => w.Brand).ThenBy(w => w.Name),
            _ => whiskies.OrderBy(w => w.Brand).ThenBy(w => w.Name),
        };

        Whiskies = await whiskies.ToListAsync();
    }
}