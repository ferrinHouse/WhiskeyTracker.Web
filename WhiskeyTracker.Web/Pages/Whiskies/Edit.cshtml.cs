using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Pages.Whiskies;

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
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GooglePhotoToken);
            var response = await httpClient.GetAsync(GooglePhotoUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
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
        }
        else if (ImageUpload != null)
        {
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageUpload.FileName;
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