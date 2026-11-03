using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Web.Pages.Whiskies;



[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    // Dependency Injection: We ask for the database context here
    public CreateModel(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // BindProperty connects the HTML form fields to this C# object automatically
    [BindProperty]
    public Whiskey NewWhiskey { get; set; } = default!;

    [BindProperty]
    public IFormFile? ImageUpload { get; set; }

    [BindProperty]
    public string? GooglePhotoUrl { get; set; }

    [BindProperty]
    public string? GooglePhotoToken { get; set; }

    public void OnGet()
    {
        // This runs when you first visit the page
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Check if the required fields (defined in Whiskey.cs) are filled
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
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
            NewWhiskey.ImageFileName = uniqueFileName;
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

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await ImageUpload.CopyToAsync(fileStream);
            }

            NewWhiskey.ImageFileName = uniqueFileName;
        }

        // Add to the database and save
        _context.Whiskies.Add(NewWhiskey);
        await _context.SaveChangesAsync();

        if (TempData != null)
        {
            TempData["PromptAddBottle"] = true;
            TempData["NewWhiskeyId"] = NewWhiskey.Id;
        }

        // For now, redirect back to the home page after saving
        return RedirectToPage("/Whiskies/Index");
    }
}