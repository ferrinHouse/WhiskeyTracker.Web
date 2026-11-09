using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Services;

/// A whiskey definition proposed by an outside source (photo scan, bulk import, MCP agent).
public class WhiskeyCandidate
{
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Distillery { get; set; }
    public string? Region { get; set; }
    public string? Type { get; set; }
    public int? Age { get; set; }
    public double? ABV { get; set; }
    public string? CaskType { get; set; }
    public string? Barcode { get; set; }
    public string? GeneralNotes { get; set; }
    /// Per-item reference (e.g. a Wikidata QID). Falls back to ImportOptions.SourceRef.
    public string? SourceRef { get; set; }
}

public class ImportOptions
{
    /// Report what would happen without writing anything. On by default so callers opt in to writes.
    public bool DryRun { get; set; } = true;
    public WhiskeyStatus Status { get; set; } = WhiskeyStatus.Pending;
    public WhiskeySource Source { get; set; } = WhiskeySource.Import;
    /// Batch-level reference, used for items without their own SourceRef so a batch can be found again.
    public string? SourceRef { get; set; }
}

public enum ImportOutcome
{
    Created,
    WouldCreate,
    Duplicate,
    Invalid
}

public enum MatchKind
{
    Exact,
    Barcode,
    Similar
}

public record WhiskeyMatch(int Id, string Brand, string Name, int? Age, WhiskeyStatus Status, MatchKind Kind);

public class NormalizedCandidate
{
    public required Whiskey Whiskey { get; init; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public class ImportItemResult
{
    public int Index { get; init; }
    public ImportOutcome Outcome { get; init; }
    public string NormalizedKey { get; init; } = string.Empty;
    /// The created whiskey, or the existing whiskey this item duplicates.
    public int? WhiskeyId { get; init; }
    /// For duplicates of an item earlier in the same batch (not yet saved, so no WhiskeyId in a dry run).
    public int? DuplicateOfIndex { get; init; }
    public WhiskeyStatus? ExistingStatus { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public class ImportResult
{
    public bool DryRun { get; init; }
    public List<ImportItemResult> Items { get; } = new();
    public int Created => Items.Count(i => i.Outcome is ImportOutcome.Created or ImportOutcome.WouldCreate);
    public int Duplicates => Items.Count(i => i.Outcome == ImportOutcome.Duplicate);
    public int Invalid => Items.Count(i => i.Outcome == ImportOutcome.Invalid);
}

/// The single place where outside whiskey data is cleaned, validated, checked for duplicates and saved.
/// Photo scans, the admin import API and the MCP server all go through it.
public class WhiskeyImportService
{
    public const int MaxBatchSize = 500;
    public const int MaxAge = 80;

    // Maps common spellings onto the vocabulary already used in the library.
    private static readonly Dictionary<string, string> TypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bourbon whiskey"] = "Bourbon",
        ["bourbon whisky"] = "Bourbon",
        ["kentucky straight bourbon whiskey"] = "Bourbon",
        ["straight bourbon whiskey"] = "Bourbon",
        ["straight bourbon"] = "Bourbon",
        ["rye whiskey"] = "Rye",
        ["rye whisky"] = "Rye",
        ["straight rye whiskey"] = "Rye",
        ["straight rye"] = "Rye",
        ["single malt scotch whisky"] = "Single Malt",
        ["single malt scotch"] = "Single Malt",
        ["single malt whisky"] = "Single Malt",
        ["single malt whiskey"] = "Single Malt",
        ["blended scotch whisky"] = "Blend",
        ["blended scotch"] = "Blend",
        ["blended whisky"] = "Blend",
        ["blended whiskey"] = "Blend",
        ["blended"] = "Blend",
        ["blended malt scotch whisky"] = "Blended Malt",
        ["blended malt"] = "Blended Malt",
        ["single pot still irish whiskey"] = "Single Pot Still",
        ["single grain scotch whisky"] = "Single Grain",
        ["single grain whisky"] = "Single Grain",
        ["tennessee whiskey"] = "Tennessee Whiskey",
    };

    private readonly AppDbContext _context;

    public WhiskeyImportService(AppDbContext context)
    {
        _context = context;
    }

    /// Cleans a candidate into a Whiskey. Out-of-range numbers are dropped with a warning rather than
    /// guessed; missing Name or Brand makes the candidate invalid.
    public NormalizedCandidate Normalize(WhiskeyCandidate candidate)
    {
        var brand = Clean(candidate.Brand, 100);
        var whiskey = new Whiskey
        {
            Name = Clean(candidate.Name, 100) ?? string.Empty,
            Brand = brand ?? string.Empty,
            // Distillery is required by the model; for most labels the brand is the best fallback.
            Distillery = Clean(candidate.Distillery, 100) ?? brand ?? string.Empty,
            Region = Clean(candidate.Region, 60) ?? string.Empty,
            Type = CanonicalType(Clean(candidate.Type, 60)) ?? string.Empty,
            CaskType = Clean(candidate.CaskType, 100),
            GeneralNotes = Clean(candidate.GeneralNotes, 2000),
            Barcode = CleanBarcode(candidate.Barcode),
            SourceRef = Clean(candidate.SourceRef, 200),
        };

        var result = new NormalizedCandidate { Whiskey = whiskey };

        if (string.IsNullOrEmpty(whiskey.Name)) result.Errors.Add("Name is required.");
        if (string.IsNullOrEmpty(whiskey.Brand)) result.Errors.Add("Brand is required.");
        if (string.IsNullOrEmpty(Clean(candidate.Distillery, 100)) && !string.IsNullOrEmpty(brand))
        {
            result.Warnings.Add("Distillery was missing; used the brand.");
        }

        if (candidate.Age is > 0 and <= MaxAge)
        {
            whiskey.Age = candidate.Age;
        }
        else if (candidate.Age is not null and not 0)
        {
            result.Warnings.Add($"Age {candidate.Age} is out of range (1-{MaxAge}) and was dropped.");
        }

        if (candidate.ABV is > 0 and <= 100)
        {
            whiskey.ABV = Math.Round(candidate.ABV.Value, 2);
        }
        else if (candidate.ABV is not null)
        {
            result.Warnings.Add($"ABV {candidate.ABV} is out of range (0-100) and was dropped.");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Barcode) && whiskey.Barcode == null)
        {
            result.Warnings.Add("Barcode was not 8-14 digits and was dropped.");
        }

        whiskey.NormalizedKey = WhiskeyKey.For(whiskey);
        return result;
    }

    /// Existing whiskeys that look like the candidate: same key, same barcode, or the same brand with
    /// an overlapping name. Includes Pending and Rejected whiskeys so they aren't proposed twice.
    public async Task<List<WhiskeyMatch>> FindMatchesAsync(WhiskeyCandidate candidate, int limit = 10)
    {
        var normalized = Normalize(candidate);
        var whiskey = normalized.Whiskey;
        var matches = new List<WhiskeyMatch>();

        if (!string.IsNullOrEmpty(whiskey.Brand) || !string.IsNullOrEmpty(whiskey.Name))
        {
            var exact = await _context.Whiskies
                .Where(w => w.NormalizedKey == whiskey.NormalizedKey)
                .Select(w => new WhiskeyMatch(w.Id, w.Brand, w.Name, w.Age, w.Status, MatchKind.Exact))
                .ToListAsync();
            matches.AddRange(exact);
        }

        if (whiskey.Barcode != null)
        {
            var byBarcode = await _context.Whiskies
                .Where(w => w.Barcode == whiskey.Barcode)
                .Select(w => new WhiskeyMatch(w.Id, w.Brand, w.Name, w.Age, w.Status, MatchKind.Barcode))
                .ToListAsync();
            matches.AddRange(byBarcode.Where(b => matches.All(m => m.Id != b.Id)));
        }

        var brandKey = WhiskeyKey.NormalizeText(whiskey.Brand);
        var nameKey = WhiskeyKey.NormalizeText(whiskey.Name);
        if (brandKey.Length > 0 && nameKey.Length > 0)
        {
            var prefix = brandKey + "|";
            var sameBrand = await _context.Whiskies
                .Where(w => w.NormalizedKey.StartsWith(prefix))
                .Select(w => new { w.Id, w.Brand, w.Name, w.Age, w.Status })
                .Take(200)
                .ToListAsync();

            foreach (var w in sameBrand)
            {
                if (matches.Any(m => m.Id == w.Id)) continue;
                var otherName = WhiskeyKey.NormalizeText(w.Name);
                if (otherName.Contains(nameKey) || nameKey.Contains(otherName))
                {
                    matches.Add(new WhiskeyMatch(w.Id, w.Brand, w.Name, w.Age, w.Status, MatchKind.Similar));
                }
            }
        }

        return matches.Take(limit).ToList();
    }

    /// Validates, de-duplicates (against the database and within the batch) and, unless DryRun is set,
    /// saves the candidates. Duplicates are skipped, never merged or overwritten.
    public async Task<ImportResult> ImportAsync(IReadOnlyList<WhiskeyCandidate> candidates, ImportOptions options)
    {
        if (candidates.Count > MaxBatchSize)
        {
            throw new ArgumentException($"A batch can hold at most {MaxBatchSize} whiskeys.", nameof(candidates));
        }

        var normalized = candidates.Select(Normalize).ToList();

        var keys = normalized.Where(n => n.IsValid).Select(n => n.Whiskey.NormalizedKey).Distinct().ToList();
        var barcodes = normalized.Where(n => n.IsValid && n.Whiskey.Barcode != null)
            .Select(n => n.Whiskey.Barcode!).Distinct().ToList();

        // Two queries for the whole batch rather than one per item.
        var existingByKey = (await _context.Whiskies
                .Where(w => keys.Contains(w.NormalizedKey))
                .Select(w => new { w.Id, w.NormalizedKey, w.Status })
                .ToListAsync())
            .GroupBy(w => w.NormalizedKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Id).First());

        var existingByBarcode = (await _context.Whiskies
                .Where(w => w.Barcode != null && barcodes.Contains(w.Barcode))
                .Select(w => new { w.Id, Barcode = w.Barcode!, w.Status })
                .ToListAsync())
            .GroupBy(w => w.Barcode)
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Id).First());

        var result = new ImportResult { DryRun = options.DryRun };
        var seenInBatch = new Dictionary<string, int>();
        var toCreate = new List<(int Index, Whiskey Whiskey)>();

        for (var i = 0; i < normalized.Count; i++)
        {
            var n = normalized[i];
            var w = n.Whiskey;

            if (!n.IsValid)
            {
                result.Items.Add(new ImportItemResult
                {
                    Index = i, Outcome = ImportOutcome.Invalid, NormalizedKey = w.NormalizedKey,
                    Errors = n.Errors, Warnings = n.Warnings
                });
                continue;
            }

            var existing = existingByKey.GetValueOrDefault(w.NormalizedKey);
            var existingBarcode = w.Barcode != null ? existingByBarcode.GetValueOrDefault(w.Barcode) : null;
            if (existing != null || existingBarcode != null)
            {
                result.Items.Add(new ImportItemResult
                {
                    Index = i, Outcome = ImportOutcome.Duplicate, NormalizedKey = w.NormalizedKey,
                    WhiskeyId = existing?.Id ?? existingBarcode!.Id,
                    ExistingStatus = existing?.Status ?? existingBarcode!.Status,
                    Warnings = n.Warnings
                });
                continue;
            }

            var batchKeys = new List<string> { "k:" + w.NormalizedKey };
            if (w.Barcode != null) batchKeys.Add("b:" + w.Barcode);
            var earlier = batchKeys.Select(k => seenInBatch.TryGetValue(k, out var idx) ? idx : (int?)null)
                .FirstOrDefault(idx => idx != null);
            if (earlier != null)
            {
                result.Items.Add(new ImportItemResult
                {
                    Index = i, Outcome = ImportOutcome.Duplicate, NormalizedKey = w.NormalizedKey,
                    DuplicateOfIndex = earlier, Warnings = n.Warnings
                });
                continue;
            }
            foreach (var k in batchKeys) seenInBatch[k] = i;

            w.Status = options.Status;
            w.Source = options.Source;
            w.SourceRef ??= Clean(options.SourceRef, 200);
            toCreate.Add((i, w));
        }

        if (!options.DryRun && toCreate.Count > 0)
        {
            _context.Whiskies.AddRange(toCreate.Select(t => t.Whiskey));
            await _context.SaveChangesAsync();
        }

        foreach (var (index, whiskey) in toCreate)
        {
            result.Items.Add(new ImportItemResult
            {
                Index = index,
                Outcome = options.DryRun ? ImportOutcome.WouldCreate : ImportOutcome.Created,
                NormalizedKey = whiskey.NormalizedKey,
                WhiskeyId = options.DryRun ? null : whiskey.Id,
                Warnings = normalized[index].Warnings
            });
        }

        result.Items.Sort((a, b) => a.Index.CompareTo(b.Index));
        return result;
    }

    /// Fills NormalizedKey for whiskeys saved before the column existed. Safe to run on every startup:
    /// once everything has a key it only runs one cheap query.
    public async Task<int> BackfillNormalizedKeysAsync()
    {
        var missing = await _context.Whiskies.Where(w => w.NormalizedKey == "").ToListAsync();
        foreach (var w in missing)
        {
            w.NormalizedKey = WhiskeyKey.For(w);
        }

        if (missing.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return missing.Count;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > maxLength ? collapsed[..maxLength].TrimEnd() : collapsed;
    }

    private static string? CanonicalType(string? type) =>
        type != null && TypeAliases.TryGetValue(type, out var canonical) ? canonical : type;

    /// UPC-E/EAN-8 through GTIN-14: digits only, 8-14 long. Anything else is dropped.
    private static string? CleanBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var digits = new string(barcode.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        return digits.Length is >= 8 and <= 14 && digits.All(char.IsAsciiDigit) ? digits : null;
    }
}
