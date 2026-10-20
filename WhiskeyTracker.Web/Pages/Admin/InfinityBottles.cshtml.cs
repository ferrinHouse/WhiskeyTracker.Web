using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Web.Pages.Admin;

public class InfinityBottlesModel : PageModel
{
    private readonly InfinityBottleMigrationService _migration;
    private readonly ILogger<InfinityBottlesModel> _logger;

    public InfinityBottlesModel(InfinityBottleMigrationService migration, ILogger<InfinityBottlesModel> logger)
    {
        _migration = migration;
        _logger = logger;
    }

    public List<InfinityBottleMigrationService.SharedInfinityBottle> SharedBottles { get; set; } = new();

    public async Task OnGetAsync()
    {
        SharedBottles = await _migration.FindSharedAsync();
    }

    public async Task<IActionResult> OnPostMigrateAsync()
    {
        try
        {
            var count = await _migration.MigrateAsync();
            TempData["Message"] = count == 0
                ? "No infinity bottles needed migrating."
                : $"Gave {count} infinity bottle(s) their own whiskey record.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Infinity bottle migration failed.");
            TempData["ErrorMessage"] = "The migration failed and was rolled back.";
        }

        return RedirectToPage();
    }
}
