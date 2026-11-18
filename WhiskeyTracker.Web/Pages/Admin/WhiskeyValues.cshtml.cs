using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Web.Pages.Admin;

/// Lets an admin merge spelling variants of a whiskey text field ("New Riff" into "New Riff Distilling")
/// so the library filters show one value. Merging is two steps: Preview shows what will change and any
/// whiskeys that would become duplicates; Merge applies it. The /Admin folder is admin-only (Program.cs).
public class WhiskeyValuesModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<WhiskeyValuesModel> _logger;

    public WhiskeyValuesModel(AppDbContext context, ILogger<WhiskeyValuesModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public WhiskeyValueField Field { get; set; } = WhiskeyValueField.Brand;

    public List<WhiskeyValueCount> Values { get; set; } = new();
    public List<List<WhiskeyValueCount>> Suggestions { get; set; } = new();
    public MergePreview? Preview { get; set; }
    public string? ErrorMessage { get; set; }

    public class MergePreview
    {
        public string Target { get; init; } = string.Empty;
        public List<WhiskeyValueCount> Sources { get; init; } = new();
        public int AffectedCount { get; init; }
        /// Whiskeys that would share Brand + Name + Age with another whiskey after the merge.
        public List<string> NewDuplicates { get; init; } = new();
    }

    public async Task OnGetAsync()
    {
        await LoadValuesAsync();
    }

    public async Task<IActionResult> OnPostPreviewAsync(string? target, string[]? sources)
    {
        await LoadValuesAsync();

        if (!TryValidate(target, sources, out var cleanTarget, out var cleanSources, out var error))
        {
            ErrorMessage = error;
            return Page();
        }

        var affected = await WhereValueIn(_context.Whiskies.AsNoTracking(), cleanSources).ToListAsync();
        if (affected.Count == 0)
        {
            ErrorMessage = "No whiskeys use the selected values any more. The list below is up to date.";
            return Page();
        }

        Preview = new MergePreview
        {
            Target = cleanTarget,
            Sources = cleanSources
                .Select(s => new WhiskeyValueCount(s, affected.Count(w => WhiskeyValueMerge.Get(w, Field) == s)))
                .Where(s => s.Count > 0)
                .ToList(),
            AffectedCount = affected.Count,
            NewDuplicates = await FindNewDuplicatesAsync(affected, cleanTarget)
        };
        return Page();
    }

    public async Task<IActionResult> OnPostMergeAsync(string? target, string[]? sources)
    {
        if (!TryValidate(target, sources, out var cleanTarget, out var cleanSources, out var error))
        {
            await LoadValuesAsync();
            ErrorMessage = error;
            return Page();
        }

        var affected = await WhereValueIn(_context.Whiskies, cleanSources).ToListAsync();
        foreach (var whiskey in affected)
        {
            WhiskeyValueMerge.Set(whiskey, Field, cleanTarget);
        }
        // AppDbContext's save hook trims the value and recomputes NormalizedKey.
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminUser} merged {Field} values {Sources} into {Target} on {Count} whiskeys",
            User.Identity?.Name, Field, cleanSources, cleanTarget, affected.Count);

        TempData["SuccessMessage"] =
            $"Merged {string.Join(", ", cleanSources.Select(s => $"'{s}'"))} into '{cleanTarget}' on {affected.Count} whiskey(s).";
        return RedirectToPage(new { Field });
    }

    private async Task LoadValuesAsync()
    {
        var rows = await SelectField(_context.Whiskies.AsNoTracking())
            .GroupBy(v => v)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync();

        Values = rows
            .Select(r => new WhiskeyValueCount(r.Value ?? string.Empty, r.Count))
            .OrderBy(v => v.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Suggestions = WhiskeyValueMerge.SuggestGroups(Values);
    }

    private bool TryValidate(string? target, string[]? sources, out string cleanTarget, out List<string> cleanSources, out string? error)
    {
        cleanTarget = target?.Trim() ?? string.Empty;
        var keep = cleanTarget;
        cleanSources = (sources ?? Array.Empty<string>())
            .Select(s => s?.Trim() ?? string.Empty)
            .Where(s => s.Length > 0 && s != keep)
            .Distinct()
            .ToList();
        error = null;

        if (cleanTarget.Length == 0)
            error = "Choose or type the value to keep.";
        else if (cleanTarget.Length > WhiskeyValueMerge.MaxLength(Field))
            error = $"{Field} can be at most {WhiskeyValueMerge.MaxLength(Field)} characters.";
        else if (cleanSources.Count == 0)
            error = "Select at least one other value to merge into it.";

        return error == null;
    }

    /// Whiskeys whose duplicate-check key (Brand + Name + Age) would match another whiskey's after the
    /// merge, where they did not match before. Only Brand is part of the key.
    private async Task<List<string>> FindNewDuplicatesAsync(List<Whiskey> affected, string target)
    {
        if (Field != WhiskeyValueField.Brand) return new();

        var affectedIds = affected.Select(w => w.Id).ToHashSet();
        var others = await _context.Whiskies.AsNoTracking()
            .Where(w => !affectedIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Brand, w.Name, w.Age, Key = w.NormalizedKey })
            .ToListAsync();

        var before = others.Select(o => (o.Id, Desc: Describe(o.Brand, o.Name, o.Age), Key: o.Key, NewKey: o.Key))
            .Concat(affected.Select(w => (w.Id, Desc: Describe(w.Brand, w.Name, w.Age), Key: WhiskeyKey.For(w), NewKey: WhiskeyKey.For(target, w.Name, w.Age))))
            .ToList();

        return before
            .GroupBy(x => x.NewKey)
            .Where(g => g.Count() > 1 && g.Any(x => affectedIds.Contains(x.Id)) && g.Select(x => x.Key).Distinct().Count() > 1)
            .Select(g => string.Join(" + ", g.Select(x => x.Desc)))
            .ToList();
    }

    private static string Describe(string brand, string name, int? age) =>
        age.HasValue ? $"{brand} {name} ({age} yr)" : $"{brand} {name}";

    private IQueryable<string> SelectField(IQueryable<Whiskey> query) => Field switch
    {
        WhiskeyValueField.Brand => query.Select(w => w.Brand),
        WhiskeyValueField.Type => query.Select(w => w.Type),
        WhiskeyValueField.Region => query.Select(w => w.Region),
        WhiskeyValueField.Distillery => query.Select(w => w.Distillery),
        _ => throw new ArgumentOutOfRangeException(nameof(Field))
    };

    private IQueryable<Whiskey> WhereValueIn(IQueryable<Whiskey> query, List<string> values) => Field switch
    {
        WhiskeyValueField.Brand => query.Where(w => values.Contains(w.Brand)),
        WhiskeyValueField.Type => query.Where(w => values.Contains(w.Type)),
        WhiskeyValueField.Region => query.Where(w => values.Contains(w.Region)),
        WhiskeyValueField.Distillery => query.Where(w => values.Contains(w.Distillery)),
        _ => throw new ArgumentOutOfRangeException(nameof(Field))
    };
}
