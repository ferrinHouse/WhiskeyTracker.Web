using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Bottles;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public class CreateInfinityBottleInput
    {
        [Required(ErrorMessage = "Please give your Infinity Bottle a name.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        [Display(Name = "Bottle / Blend Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Brand / Maker (Optional)")]
        public string? Brand { get; set; }

        [StringLength(50)]
        [Display(Name = "Whiskey Type")]
        public string? Type { get; set; } = "Infinity Blend";

        [StringLength(100)]
        [Display(Name = "Region (Optional)")]
        public string? Region { get; set; }

        [StringLength(500)]
        [Display(Name = "Description / Notes")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a collection.")]
        [Display(Name = "Collection")]
        public int CollectionId { get; set; }

        [Required]
        [Range(100, 5000, ErrorMessage = "Capacity must be between 100 and 5000 ml.")]
        [Display(Name = "Capacity (ml)")]
        public int CapacityMl { get; set; } = 750;

        [Required]
        [Range(0, 5000, ErrorMessage = "Starting volume must be between 0 and 5000 ml.")]
        [Display(Name = "Starting Volume (ml)")]
        public int CurrentVolumeMl { get; set; } = 0;

        [Display(Name = "Bottle Status")]
        public BottleStatus Status { get; set; } = BottleStatus.Opened;

        [Display(Name = "Copy Bottle Image From (Optional)")]
        public int? CopyImageFromWhiskeyId { get; set; }
    }

    [BindProperty]
    public CreateInfinityBottleInput Input { get; set; } = new();

    public SelectList Collections { get; set; } = default!;
    public SelectList ImageSourceWhiskies { get; set; } = default!;

    private async Task PopulateDropdownsAsync(string userId)
    {
        var myCollections = await _context.CollectionMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.Collection!)
            .OrderBy(c => c.Name)
            .ToListAsync();

        Collections = new SelectList(myCollections, nameof(Collection.Id), nameof(Collection.Name), Input.CollectionId);

        var whiskies = await _context.Whiskies
            .Where(w => w.ImageFileName != null)
            .OrderBy(w => w.Brand)
            .ThenBy(w => w.Name)
            .Select(w => new { w.Id, Display = $"{w.Brand} - {w.Name}" })
            .ToListAsync();

        ImageSourceWhiskies = new SelectList(whiskies, "Id", "Display", Input.CopyImageFromWhiskeyId);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        await PopulateDropdownsAsync(userId);

        if (!Collections.Any())
        {
            return Page();
        }

        // Default to first collection if not set
        if (Input.CollectionId == 0)
        {
            var firstCollection = Collections.FirstOrDefault();
            if (firstCollection != null && int.TryParse(firstCollection.Value, out var firstId))
            {
                Input.CollectionId = firstId;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Security check: User must be a member of the selected collection
        var isMember = await _context.CollectionMembers
            .AnyAsync(cm => cm.UserId == userId && cm.CollectionId == Input.CollectionId);

        if (!isMember)
        {
            ModelState.AddModelError("Input.CollectionId", "You do not have permission to add bottles to this collection.");
        }

        if (Input.CurrentVolumeMl > Input.CapacityMl)
        {
            ModelState.AddModelError("Input.CurrentVolumeMl", "Starting volume cannot exceed bottle capacity.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(userId);
            return Page();
        }

        string? imageFileName = null;
        if (Input.CopyImageFromWhiskeyId.HasValue)
        {
            var source = await _context.Whiskies.FindAsync(Input.CopyImageFromWhiskeyId.Value);
            if (source == null || string.IsNullOrEmpty(source.ImageFileName))
            {
                ModelState.AddModelError("Input.CopyImageFromWhiskeyId", "Selected whiskey has no image to copy.");
                await PopulateDropdownsAsync(userId);
                return Page();
            }
            imageFileName = WhiskeyTracker.Web.Services.ImageFileCopier.Copy(_environment.WebRootPath, source.ImageFileName);
        }

        var newWhiskey = new Whiskey
        {
            Name = Input.Name.Trim(),
            Brand = string.IsNullOrWhiteSpace(Input.Brand) ? "Living Blend" : Input.Brand.Trim(),
            Type = string.IsNullOrWhiteSpace(Input.Type) ? "Infinity Blend" : Input.Type.Trim(),
            Region = string.IsNullOrWhiteSpace(Input.Region) ? "Blend" : Input.Region.Trim(),
            Distillery = "Home Blend",
            GeneralNotes = Input.Description?.Trim(),
            ImageFileName = imageFileName
        };

        _context.Whiskies.Add(newWhiskey);
        await _context.SaveChangesAsync();

        var whiskeyId = newWhiskey.Id;
        var bottleName = newWhiskey.Name;

        var bottle = new Bottle
        {
            WhiskeyId = whiskeyId,
            CollectionId = Input.CollectionId,
            UserId = userId,
            IsInfinityBottle = true,
            Status = Input.Status,
            CapacityMl = Input.CapacityMl,
            CurrentVolumeMl = Input.CurrentVolumeMl,
            PurchaseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PurchaseLocation = "Infinity Bottle Decanter"
        };

        _context.Bottles.Add(bottle);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Infinity Bottle '{bottleName}' created successfully!";
        return RedirectToPage("/Bottles/Infinity", new { id = bottle.Id });
    }
}
