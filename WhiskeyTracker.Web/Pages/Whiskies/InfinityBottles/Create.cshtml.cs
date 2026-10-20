using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WhiskeyTracker.Web.Pages.Whiskies.InfinityBottles;

public class CreateModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Bottles/Create");
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Bottles/Create");
    }
}
