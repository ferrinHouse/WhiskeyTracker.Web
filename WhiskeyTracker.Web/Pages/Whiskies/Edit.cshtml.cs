using Microsoft.AspNetCore.Authorization;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Web.Pages.Whiskies;



[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public EditModel(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [BindProperty]
    public Whiskey Whiskey { get; set; } = default!;

    [BindProperty]
    public IFormFile? ImageUpload { get; set; }

    [BindProperty]
    public string? GooglePhotoUrl { get; set; }

    [BindProperty]
    public string? GooglePhotoToken { get; set; }

    // "int? id" means the ID is optional in the URL, but we check if it's null
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        // Find the whiskey in the database
        var whiskey = await _context.Whiskies.FirstOrDefaultAsync(m => m.Id == id);

        if (whiskey == null)
        {
            return NotFound();
        }

        Whiskey = whiskey;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!string.IsNullOrEmpty(GooglePhotoUrl) && !string.IsNullOrEmpty(GooglePhotoToken))
        {
            if (!ImageUploadValidator.IsAllowedGooglePhotoUrl(GooglePhotoUrl))
            {
                ModelState.AddModelError("GooglePhotoUrl", "Invalid Google Photo URL. URL must use HTTPS and point to a trusted Google domain.");
                return Page();
            }

            var imageBytes = await ImageUploadValidator.DownloadGooglePhotoAsync(GooglePhotoUrl, GooglePhotoToken);
            if (imageBytes == null)
            {
                ModelState.AddModelError("GooglePhotoUrl", $"Could not download the Google Photo. It must be an image no larger than {ImageUploadValidator.MaxImageBytes / (1024 * 1024)} MB.");
                return Page();
            }

            var uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images");
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            if (!string.IsNullOrEmpty(Whiskey.ImageFileName))
            {
                var oldPath = Path.Combine(uploadsFolder, Whiskey.ImageFileName);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            Whiskey.ImageFileName = uniqueFileName;
        }
        else if (ImageUpload != null)
        {
            var uploadError = ImageUploadValidator.ValidateUpload(ImageUpload);
            if (uploadError != null)
            {
                ModelState.AddModelError("ImageUpload", uploadError);
                return Page();
            }

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageUpload.FileName);
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images");
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await ImageUpload.CopyToAsync(fileStream);
            }

            if (!string.IsNullOrEmpty(Whiskey.ImageFileName))
            {
                var oldPath = Path.Combine(uploadsFolder, Whiskey.ImageFileName);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            Whiskey.ImageFileName = uniqueFileName;
        }

        // Tell the database that this entity has been modified
        _context.Attach(Whiskey).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Whiskies.Any(e => e.Id == Whiskey.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return RedirectToPage("./Index");
    }
}