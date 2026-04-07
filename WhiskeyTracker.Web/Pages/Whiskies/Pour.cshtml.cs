using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

public class PourModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PourModel(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    [BindProperty]
    public int SourceBottleId { get; set;}

    [BindProperty]
    public int TargetInfinityBottleId { get; set; }

    [BindProperty]
    [Display(Name = "Pour Amount (oz)")]
    [Range(0.1, 25.0, ErrorMessage = "Please enter a valid pour amount between 0.1 and 25 oz.")]
    public double PourAmountOz { get; set; } = 2.0;

    public Bottle SourceBottle { get; set; } = default!;
    public SelectList InfinityBottles { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var bottle = await _context.Bottles
            .Include(b => b.Whiskey)
            .Include(b => b.Collection)
            .FirstOrDefaultAsync(m => m.Id == id);

        // Security Check: Ensure I am a member of the bottle's collection
        if (bottle == null) return NotFound();
        
        var canAccess = await _context.CollectionMembers.AnyAsync(m => m.UserId == userId && m.CollectionId == bottle.CollectionId);
        if (!canAccess) return NotFound();

        SourceBottle = bottle;
        SourceBottleId = bottle.Id;

        PourAmountOz = Math.Round(bottle.CurrentVolumeMl / 29.5735, 1);

        // Find Infinity Bottles in ANY collection I am a member of
        // This allows cross-collection pouring? Maybe restricting to SAME collection is safer?
        // Let's allow ANY collection I'm in for maximum flexibility (e.g. pour from my personal stash to the House Infinity)
        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        var infinityBottles = await _context.Bottles
            .Include(b => b.Whiskey)
            .Where(b => b.IsInfinityBottle && b.Id != bottle.Id && b.Status != BottleStatus.Full && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
            .ToListAsync();

        InfinityBottles = new SelectList(infinityBottles, "Id", "Whiskey.Name");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        // Optimize: Get my collections once
        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        var source = await _context.Bottles
            .Include(b => b.Whiskey)
            .FirstOrDefaultAsync(b => b.Id == SourceBottleId && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));

        if (source == null) return NotFound();

        if (!ModelState.IsValid)
        {
            SourceBottle = source;
            var infinityBottles = await _context.Bottles
                .Include(b => b.Whiskey)
                .Where(b => b.IsInfinityBottle && b.Id != source.Id && b.Status != BottleStatus.Full && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
                .ToListAsync();
            InfinityBottles = new SelectList(infinityBottles, "Id", "Whiskey.Name");
            return Page();
        }

        var target = await _context.Bottles
            .Include(b => b.Whiskey)
            .FirstOrDefaultAsync(b => b.Id == TargetInfinityBottleId && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));

        if (target == null) return NotFound();

        var pourMl = (int)Math.Round(PourAmountOz * 29.5735);

        source.CurrentVolumeMl = 0;
        source.Status = BottleStatus.Empty;

        target.CurrentVolumeMl += pourMl;

        var blendLog = new BlendComponent
        {
            SourceBottleId = source.Id,
            InfinityBottleId = target.Id,
            AmountAddedMl = pourMl,
            DateAdded = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)
        };
        _context.BlendComponents.Add(blendLog);
        await _context.SaveChangesAsync();
        return RedirectToPage("/Whiskies/Details", new { id = source.WhiskeyId });
    }
}