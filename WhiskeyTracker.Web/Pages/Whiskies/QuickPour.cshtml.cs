using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

public class QuickPourModel : PageModel
{
    private readonly AppDbContext _context;

    public QuickPourModel(AppDbContext context)
    {
        _context = context;
    }

    public Bottle Bottle { get; set; } = default!;

    [BindProperty]
    [Display(Name = "Pour Amount (oz)")]
    [Range(0.1, 25.0, ErrorMessage = "Please enter a valid pour amount between 0.1 and 25 oz.")]
    public double PourAmountOz { get; set; } = 2.0;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var bottle = await _context.Bottles
            .Include(b => b.Whiskey)
            .Include(b => b.Collection)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bottle == null) return NotFound();

        var canAccess = await _context.CollectionMembers
            .AnyAsync(m => m.UserId == userId && m.CollectionId == bottle.CollectionId);

        if (!canAccess) return NotFound();

        if (bottle.Status == BottleStatus.Empty)
            return RedirectToPage("./Details", new { id = bottle.WhiskeyId });

        Bottle = bottle;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        var bottle = await _context.Bottles
            .Include(b => b.Whiskey)
            .FirstOrDefaultAsync(b => b.Id == id && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));

        if (bottle == null) return NotFound();

        if (!ModelState.IsValid)
        {
            Bottle = bottle;
            return Page();
        }

        var pourMl = (int)Math.Round(PourAmountOz * 29.5735);

        if (bottle.Status == BottleStatus.Full)
            bottle.Status = BottleStatus.Opened;

        bottle.CurrentVolumeMl -= pourMl;

        if (bottle.CurrentVolumeMl <= 0)
        {
            bottle.CurrentVolumeMl = 0;
            bottle.Status = BottleStatus.Empty;
            TempData["InfoMessage"] = "You killed the bottle! It has been marked as Empty.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Poured {PourAmountOz:0.#} oz. {bottle.CurrentVolumeMl} ml remaining.";
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("./Details", new { id = bottle.WhiskeyId });
    }
}
