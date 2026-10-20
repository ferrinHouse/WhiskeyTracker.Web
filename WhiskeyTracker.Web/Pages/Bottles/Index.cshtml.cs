using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Bottles;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public class InfinityBottleItem
    {
        public int BottleId { get; set; }
        public int WhiskeyId { get; set; }
        public string WhiskeyName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ImageFileName { get; set; }
        public string? CollectionName { get; set; }
        public int? CollectionId { get; set; }
        public BottleStatus Status { get; set; }
        public int CapacityMl { get; set; }
        public int CurrentVolumeMl { get; set; }
        public int ComponentCount { get; set; }
        public int TotalMlAdded { get; set; }
        public DateOnly? LastDateAdded { get; set; }
        public DateOnly? PurchaseDate { get; set; }

        public double FillPercentage => CapacityMl > 0 ? Math.Clamp((double)CurrentVolumeMl / CapacityMl * 100, 0, 100) : 0;
    }

    public List<InfinityBottleItem> InfinityBottles { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchString { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CollectionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public BottleStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortOrder { get; set; }

    public SelectList Collections { get; set; } = default!;

    public int TotalBottlesCount { get; set; }
    public int TotalVolumeMl { get; set; }
    public int TotalAdditionsCount { get; set; }

    public bool HasFilterActive()
    {
        return !string.IsNullOrEmpty(SearchString) ||
               CollectionId.HasValue ||
               Status.HasValue;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var myCollections = await _context.CollectionMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.Collection!)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var myCollectionIds = myCollections.Select(c => c.Id).ToList();

        if (CollectionId.HasValue && !myCollectionIds.Contains(CollectionId.Value))
        {
            CollectionId = null;
        }

        Collections = new SelectList(myCollections, nameof(Collection.Id), nameof(Collection.Name), CollectionId);

        var query = _context.Bottles
            .Include(b => b.Whiskey)
            .Include(b => b.Collection)
            .Where(b => b.IsInfinityBottle && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
            .AsQueryable();

        // Calculate summary metrics across all user's infinity bottles before filters
        var allUserInfinityBottles = await query.ToListAsync();
        TotalBottlesCount = allUserInfinityBottles.Count;
        TotalVolumeMl = allUserInfinityBottles.Sum(b => b.CurrentVolumeMl);

        // Apply filters
        if (CollectionId.HasValue)
        {
            query = query.Where(b => b.CollectionId == CollectionId.Value);
        }

        if (Status.HasValue)
        {
            query = query.Where(b => b.Status == Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(SearchString))
        {
            var searchLower = SearchString.Trim().ToLower();
            query = query.Where(b => (b.Whiskey != null && b.Whiskey.Name.ToLower().Contains(searchLower)) ||
                                     (b.Whiskey != null && b.Whiskey.Brand.ToLower().Contains(searchLower)));
        }

        var filteredBottles = await query.ToListAsync();
        var bottleIds = filteredBottles.Select(b => b.Id).ToList();

        // Query blend component stats
        var blendStats = await _context.BlendComponents
            .Where(bc => bottleIds.Contains(bc.InfinityBottleId))
            .GroupBy(bc => bc.InfinityBottleId)
            .Select(g => new
            {
                BottleId = g.Key,
                Count = g.Count(),
                TotalMl = g.Sum(bc => bc.AmountAddedMl),
                LastAdded = g.Max(bc => (DateOnly?)bc.DateAdded)
            })
            .ToDictionaryAsync(s => s.BottleId);

        TotalAdditionsCount = blendStats.Values.Sum(s => s.Count);

        var items = filteredBottles.Select(b =>
        {
            blendStats.TryGetValue(b.Id, out var stat);
            return new InfinityBottleItem
            {
                BottleId = b.Id,
                WhiskeyId = b.WhiskeyId,
                WhiskeyName = b.Whiskey?.Name ?? "Unnamed Blend",
                Brand = b.Whiskey?.Brand ?? string.Empty,
                Type = b.Whiskey?.Type ?? "Blend",
                ImageFileName = b.Whiskey?.ImageFileName,
                CollectionName = b.Collection?.Name,
                CollectionId = b.CollectionId,
                Status = b.Status,
                CapacityMl = b.CapacityMl,
                CurrentVolumeMl = b.CurrentVolumeMl,
                ComponentCount = stat?.Count ?? 0,
                TotalMlAdded = stat?.TotalMl ?? 0,
                LastDateAdded = stat?.LastAdded,
                PurchaseDate = b.PurchaseDate
            };
        }).ToList();

        // Sort items
        InfinityBottles = SortOrder switch
        {
            "name_desc" => items.OrderByDescending(i => i.WhiskeyName).ToList(),
            "volume_desc" => items.OrderByDescending(i => i.CurrentVolumeMl).ThenBy(i => i.WhiskeyName).ToList(),
            "volume_asc" => items.OrderBy(i => i.CurrentVolumeMl).ThenBy(i => i.WhiskeyName).ToList(),
            "components_desc" => items.OrderByDescending(i => i.ComponentCount).ThenBy(i => i.WhiskeyName).ToList(),
            "recent" => items.OrderByDescending(i => i.LastDateAdded ?? DateOnly.MinValue).ThenBy(i => i.WhiskeyName).ToList(),
            _ => items.OrderBy(i => i.WhiskeyName).ToList()
        };

        return Page();
    }
}
