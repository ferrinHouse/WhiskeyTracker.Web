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

    /// When set, the POST updates this existing note instead of creating a new one.
    [BindProperty]
    public int? EditNoteId { get; set; }
    
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

    // POST: Handles both "Log & Pour Next" and inline editing
    public async Task<IActionResult> OnPostAsync(int sessionId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var sessionExists = await _context.TastingSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId);
        if (!sessionExists) return NotFound();

        var myCollectionIds = await _context.CollectionMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CollectionId)
            .ToListAsync();

        // --- EDIT MODE: update an existing note ---
        if (EditNoteId.HasValue)
        {
            // Remove navigation-property errors that ASP.NET Core can't bind
            ModelState.Remove("NewNote.TastingSession");
            ModelState.Remove("NewNote.Whiskey");
            ModelState.Remove("NewNote.Bottle");

            if (!ModelState.IsValid)
            {
                await LoadPageData(sessionId);
                return Page();
            }

            var note = await _context.TastingNotes
                .Include(n => n.Bottle)
                .FirstOrDefaultAsync(n => n.Id == EditNoteId.Value && n.TastingSessionId == sessionId);

            if (note == null) return NotFound();

            var newPourMl = PourAmountOz.HasValue ? (int)Math.Round(PourAmountOz.Value * 29.5735) : note.PourAmountMl;

            if (SelectedBottleId.HasValue)
            {
                var newBottle = await _context.Bottles
                    .FirstOrDefaultAsync(b => b.Id == SelectedBottleId.Value && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value));

                if (newBottle != null)
                {
                    // Restore old bottle volume if switching to a different bottle
                    if (note.Bottle != null && note.BottleId != newBottle.Id)
                    {
                        note.Bottle.CurrentVolumeMl += note.PourAmountMl;
                        note.Bottle.Status = note.Bottle.CurrentVolumeMl > 0 ? BottleStatus.Opened : BottleStatus.Empty;
                        // Apply fresh pour to new bottle
                        newBottle.CurrentVolumeMl = Math.Max(0, newBottle.CurrentVolumeMl - newPourMl);
                        if (newBottle.Status == BottleStatus.Full) newBottle.Status = BottleStatus.Opened;
                    }
                    else
                    {
                        // Same bottle — adjust by difference only
                        var diff = newPourMl - note.PourAmountMl;
                        newBottle.CurrentVolumeMl = Math.Max(0, newBottle.CurrentVolumeMl - diff);
                    }
                    newBottle.Status = newBottle.CurrentVolumeMl == 0 ? BottleStatus.Empty : BottleStatus.Opened;
                    note.WhiskeyId = newBottle.WhiskeyId;
                    note.BottleId = newBottle.Id;
                }
                else
                {
                    ModelState.AddModelError("SelectedBottleId", "Invalid Bottle Selection (Access Denied).");
                    await LoadPageData(sessionId);
                    return Page();
                }
            }
            else if (SelectedWhiskeyId.HasValue)
            {
                // Switching to whiskey-only — restore old bottle volume
                if (note.Bottle != null)
                {
                    note.Bottle.CurrentVolumeMl += note.PourAmountMl;
                    note.Bottle.Status = note.Bottle.CurrentVolumeMl > 0 ? BottleStatus.Opened : BottleStatus.Empty;
                    note.BottleId = null;
                }
                note.WhiskeyId = SelectedWhiskeyId.Value;
            }

            note.PourAmountMl = newPourMl;
            note.Rating = NewNote.Rating;
            note.Notes = NewNote.Notes;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Pour updated!";
            return RedirectToPage(new { sessionId });
        }

        // --- ADD MODE: log a new pour ---

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
        NewNote.UserId = userId;
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
            .Where(b => b.Status != BottleStatus.Empty && b.CurrentVolumeMl > 0 && b.CollectionId.HasValue && myCollectionIds.Contains(b.CollectionId.Value))
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