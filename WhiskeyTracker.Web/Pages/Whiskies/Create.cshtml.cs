using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

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
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GooglePhotoToken);
            var response = await httpClient.GetAsync(GooglePhotoUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                NewWhiskey.ImageFileName = uniqueFileName;
            }
        }
        else if (ImageUpload != null)
        {
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageUpload.FileName;
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