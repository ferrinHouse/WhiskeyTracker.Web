using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public int TotalUsers { get; set; }
    public int TotalWhiskies { get; set; }
    public int TotalBottles { get; set; }
    public int TotalCollections { get; set; }
    public int TotalTastingNotes { get; set; }

    public async Task OnGetAsync()
    {
        TotalUsers = await _context.Users.CountAsync();
        TotalWhiskies = await _context.Whiskies.CountAsync();
        TotalBottles = await _context.Bottles.CountAsync();
        TotalCollections = await _context.Collections.CountAsync();
        TotalTastingNotes = await _context.TastingNotes.CountAsync();
    }
}
