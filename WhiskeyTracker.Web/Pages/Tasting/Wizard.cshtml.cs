using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualBasic;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Tasting;

public class WizardModel : PageModel
{
    private readonly AppDbContext _context;

    public WizardModel(AppDbContext context)
    {
        _context = context;
    }

    public TastingSession Session { get; set; } = new();
    
    [BindProperty]
    public TastingNote NewNote { get; set; } = new();

    [BindProperty]
    [Display(Name = "Pour Amount (oz)")]
    [Range(0.1, 25.0, ErrorMessage = "Please enter a valid pour amount between 0.1 and 25 oz.")]
    public double? PourAmountOz { get; set; }

    [BindProperty]
    public int? SelectedBottleId { get; set; }
    [BindProperty]
    public int? SelectedWhiskeyId { get; set; }
    
    public SelectList BottleOptions { get; set; } = default!;
    public SelectList WhiskeyOptions { get; set; } = default!;

    // GET: Prepares the page for viewing
    public async Task<IActionResult> OnGetAsync(int sessionId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        await LoadPageData(sessionId);

        if (Session.Id == 0)
        {
            return NotFound($"Session with ID {sessionId} not found.");
        }
        
        if (Session.UserId != userId) return NotFound();

        return Page();
    }

    // POST: Handles the "Log & Pour Next" button
    public async Task<IActionResult> OnPostAsync(int sessionId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var sessionExists = await _context.TastingSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId);
        if (!sessionExists) return NotFound();

        // Get my collections
        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        if (SelectedBottleId.HasValue)
        {
            // Verify bottle is in one of my collections
            var bottle = await _context.Bottles
                .FirstOrDefaultAsync(b => b.Id == SelectedBottleId.Value && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));
                
            if (bottle != null)
            {
                NewNote.WhiskeyId = bottle.WhiskeyId;
                NewNote.BottleId = bottle.Id;

                // Conversion: 1 oz = 29.5735 ml
                if (PourAmountOz.HasValue && PourAmountOz.Value > 0)
                {
                    NewNote.PourAmountMl = (int)Math.Round(PourAmountOz.Value * 29.5735);
                }

                if (NewNote.PourAmountMl > 0)
                {
                    if (bottle.Status == BottleStatus.Full)
                    {
                        bottle.Status = BottleStatus.Opened;
                    }

                    bottle.CurrentVolumeMl -= NewNote.PourAmountMl;
                    if (bottle.CurrentVolumeMl <= 0) bottle.CurrentVolumeMl = 0;
                    if (bottle.CurrentVolumeMl == 0)
                    {
                        bottle.Status = BottleStatus.Empty;
                        TempData["InfoMessage"] = "You killed the bottle! 💀 It has been marked as Finished.";
                    }
                }
            }
            else
            {
                 ModelState.AddModelError("SelectedBottleId", "Invalid Bottle Selection (Access Denied).");
            }
        }
        else if (SelectedWhiskeyId.HasValue)
        {
            NewNote.WhiskeyId = SelectedWhiskeyId.Value;
            NewNote.BottleId = null;

            if (PourAmountOz.HasValue && PourAmountOz.Value > 0)
            {
                NewNote.PourAmountMl = (int)Math.Round(PourAmountOz.Value * 29.5735);
            }
        }
        else
        {
            ModelState.AddModelError("SelectedBottleId", "You must select either a Bottle or a Whiskey.");
            ModelState.AddModelError("SelectedWhiskeyId", "You must select either a Bottle or a Whiskey.");
        }

        if (string.IsNullOrWhiteSpace(NewNote.Notes))
        {
            ModelState.AddModelError("NewNote.Notes", "Tasting notes are required.");
        }

        // Re-establish relationships
        NewNote.TastingSessionId = sessionId;
        NewNote.OrderIndex = await _context.TastingNotes.CountAsync(n => n.TastingSessionId == sessionId) + 1;

        // Validation exclusions
        ModelState.Remove("NewNote.TastingSession");
        ModelState.Remove("NewNote.Whiskey");
        ModelState.Remove("NewNote.Bottle");

        if (!ModelState.IsValid)
        {
            await LoadPageData(sessionId);
            return Page();
        }

        _context.TastingNotes.Add(NewNote);
        NewNote.UserId = userId; // Ensure consistent usage
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tasting note added successfully! Ready for the next pour!";

        // Redirect to the SAME page to refresh the list and clear the form
        return RedirectToPage(new { sessionId });
    }

    private async Task LoadPageData(int sessionId)
    {
        var session = await _context.TastingSessions
            .Include(s => s.Notes)
            .ThenInclude(n => n.Whiskey) // Include Whiskey so we can see names in the list
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session != null)
        {
            Session = session;
        }
        NewNote = new TastingNote();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Get my collections
        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        var bottles = await _context.Bottles
            .Include(b => b.Whiskey)
            .Where(b => b.Status != BottleStatus.Empty && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
            .OrderBy(b => b.Whiskey != null ? b.Whiskey.Name : string.Empty)
            .ToListAsync();

        BottleOptions = new SelectList(bottles.Select(b =>
        {
            var whiskeyName = b.Whiskey != null ? b.Whiskey.Name : "Unknown Bottle";
            var distillery = b.Whiskey != null ? b.Whiskey.Distillery : "Unknown";

            return new {
            b.Id,
            Text = $"{distillery} - {whiskeyName} ({b.Status})"
            };
        }), "Id", "Text");

        var whiskies = await _context.Whiskies
            .OrderBy(w => w.Name)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync();

        WhiskeyOptions = new SelectList(whiskies, "Id", "Name");
    }
}