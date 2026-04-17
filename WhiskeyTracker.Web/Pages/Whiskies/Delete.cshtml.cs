using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

[Authorize(Roles = "Admin")]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Whiskey Whiskey { get; set; } = default!;

    // 1. Load the data to show the user what they are deleting
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var whiskey = await _context.Whiskies.FirstOrDefaultAsync(m => m.Id == id);

        if (whiskey == null) return NotFound();

        Whiskey = whiskey;
        return Page();
    }

    // 2. Perform the actual deletion
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id == null) return NotFound();

        var whiskey = await _context.Whiskies.FindAsync(id);

        if (whiskey == null)
        {
            return NotFound();
        }
        else
        {
            _context.Whiskies.Remove(whiskey);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Admin/Whiskies");
    }
}