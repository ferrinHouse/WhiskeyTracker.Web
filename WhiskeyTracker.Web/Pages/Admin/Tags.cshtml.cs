using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TagsModel : PageModel
{
    private readonly AppDbContext _context;

    public TagsModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Tag> PendingTags { get; set; } = new();
    public List<Tag> ApprovedTags { get; set; } = new();

    public async Task OnGetAsync()
    {
        PendingTags = await _context.Tags
            .Where(t => !t.IsApproved)
            .OrderBy(t => t.Name)
            .ToListAsync();

        ApprovedTags = await _context.Tags
            .Where(t => t.IsApproved)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var tag = await _context.Tags.FindAsync(id);
        if (tag != null)
        {
            tag.IsApproved = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Tag '{tag.Name}' approved!";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tag = await _context.Tags.FindAsync(id);
        if (tag != null)
        {
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Tag '{tag.Name}' deleted!";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMergeAsync(int sourceId, int targetId)
    {
        if (sourceId == targetId) return RedirectToPage();

        var sourceTag = await _context.Tags.FindAsync(sourceId);
        var targetTag = await _context.Tags.FindAsync(targetId);

        if (sourceTag == null || targetTag == null) return RedirectToPage();

        // Find all tasting note associations for the source tag
        var associations = await _context.TastingNoteTags
            .Where(tnt => tnt.TagId == sourceId)
            .ToListAsync();

        foreach (var assoc in associations)
        {
            // Check if the note already has the target tag in the same field
            var exists = await _context.TastingNoteTags.AnyAsync(tnt => 
                tnt.TastingNoteId == assoc.TastingNoteId && 
                tnt.TagId == targetId && 
                tnt.Field == assoc.Field);

            if (!exists)
            {
                // Remap to target tag
                _context.TastingNoteTags.Add(new TastingNoteTag
                {
                    TastingNoteId = assoc.TastingNoteId,
                    TagId = targetId,
                    Field = assoc.Field
                });
            }
            
            // Remove old association
            _context.TastingNoteTags.Remove(assoc);
        }

        // Delete source tag
        _context.Tags.Remove(sourceTag);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Merged '{sourceTag.Name}' into '{targetTag.Name}'.";
        
        return RedirectToPage();
    }
}
