using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace WhiskeyTracker.Web.Pages.Collections;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Collection Collection { get; set; } = null!;
    public List<CollectionMember> Members { get; set; } = new();
    public List<CollectionInvitation> PendingInvitations { get; set; } = new();
    public bool IsOwner { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Challenge();

        var collection = await _context.Collections
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (collection == null) return NotFound();
        Collection = collection;

        // Check if user is a member
        var membership = Collection.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership == null) return Forbid();

        IsOwner = membership.Role == CollectionRole.Owner;
        Members = Collection.Members;

        if (IsOwner)
        {
            PendingInvitations = await _context.CollectionInvitations
                .Where(i => i.CollectionId == id && i.Status == InvitationStatus.Pending)
                .ToListAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Challenge();

        var collection = await _context.Collections.FirstOrDefaultAsync(c => c.Id == id);
        if (collection == null) return NotFound();

        var isMember = await _context.CollectionMembers.AnyAsync(m => m.CollectionId == id && m.UserId == userId);
        if (!isMember) return Forbid();

        var bottles = await _context.Bottles
            .AsNoTracking()
            .Include(b => b.Whiskey)
            .Where(b => b.CollectionId == id)
            .OrderBy(b => b.Whiskey!.Brand).ThenBy(b => b.Whiskey!.Name)
            .ToListAsync();

        var csv = Services.CollectionCsvExporter.Build(bottles);
        var safeName = new string(collection.Name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        if (safeName.Length == 0) safeName = "collection";

        // UTF-8 BOM so Excel reads accented characters correctly; single buffer, no LINQ copy.
        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        var body = System.Text.Encoding.UTF8.GetBytes(csv);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        var date = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return File(bytes, "text/csv", $"{safeName}-{date}.csv");
    }
}
