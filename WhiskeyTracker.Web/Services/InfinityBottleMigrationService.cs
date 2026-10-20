using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Services;

/// Gives infinity bottles their own Whiskey record, copying only the bottle image from the
/// whiskey they currently point at.
public class InfinityBottleMigrationService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public InfinityBottleMigrationService(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public class SharedInfinityBottle
    {
        public int BottleId { get; set; }
        public int WhiskeyId { get; set; }
        public string WhiskeyName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? CollectionName { get; set; }
        public int SharedWithBottleCount { get; set; }
        public bool IsShared => SharedWithBottleCount > 0;
    }

    /// Every infinity bottle, with how many other bottles share its Whiskey record.
    public async Task<List<SharedInfinityBottle>> FindAllAsync()
    {
        return await _context.Bottles
            .Where(b => b.IsInfinityBottle)
            .Select(b => new SharedInfinityBottle
            {
                BottleId = b.Id,
                WhiskeyId = b.WhiskeyId,
                WhiskeyName = b.Whiskey!.Name,
                Brand = b.Whiskey.Brand,
                CollectionName = b.Collection != null ? b.Collection.Name : null,
                SharedWithBottleCount = _context.Bottles.Count(o => o.WhiskeyId == b.WhiskeyId && o.Id != b.Id)
            })
            .OrderBy(b => b.WhiskeyName)
            .ToListAsync();
    }

    /// Infinity bottles whose Whiskey is also used by at least one other bottle.
    public async Task<List<SharedInfinityBottle>> FindSharedAsync()
    {
        return (await FindAllAsync()).Where(b => b.IsShared).ToList();
    }

    /// Gives one infinity bottle its own Whiskey, optionally under a new name.
    /// Returns false if the bottle doesn't exist or isn't an infinity bottle.
    public async Task<bool> MigrateBottleAsync(int bottleId, string? newName = null)
    {
        var exists = await _context.Bottles.AnyAsync(b => b.Id == bottleId && b.IsInfinityBottle);
        if (!exists) return false;

        await MigrateAsync(new[] { bottleId }, newName);
        return true;
    }

    /// Migrates every shared infinity bottle. Returns how many were given their own Whiskey.
    public async Task<int> MigrateAsync()
    {
        var shared = await FindSharedAsync();
        if (shared.Count == 0) return 0;

        return await MigrateAsync(shared.Select(s => s.BottleId).ToList(), null);
    }

    private async Task<int> MigrateAsync(IReadOnlyCollection<int> bottleIds, string? newName)
    {
        var bottles = await _context.Bottles
            .Include(b => b.Whiskey)
            .Where(b => bottleIds.Contains(b.Id))
            .ToListAsync();

        var copiedFiles = new List<string>();
        // The in-memory test provider has no transaction support.
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            foreach (var bottle in bottles)
            {
                var source = bottle.Whiskey!;

                string? imageFileName = null;
                if (!string.IsNullOrEmpty(source.ImageFileName))
                {
                    imageFileName = ImageFileCopier.Copy(_environment.WebRootPath, source.ImageFileName);
                    if (imageFileName != null) copiedFiles.Add(imageFileName);
                }

                var blend = new Whiskey
                {
                    Name = string.IsNullOrWhiteSpace(newName) ? source.Name : newName.Trim(),
                    Brand = "Living Blend",
                    Type = "Infinity Blend",
                    Region = "Blend",
                    Distillery = "Home Blend",
                    ImageFileName = imageFileName
                };
                _context.Whiskies.Add(blend);
                await _context.SaveChangesAsync();

                bottle.WhiskeyId = blend.Id;

                // Tastings logged from this bottle belong to the blend, not the original whiskey.
                var notes = await _context.TastingNotes.Where(n => n.BottleId == bottle.Id).ToListAsync();
                foreach (var note in notes) note.WhiskeyId = blend.Id;

                await _context.SaveChangesAsync();
            }

            if (transaction != null) await transaction.CommitAsync();
            return bottles.Count;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            foreach (var file in copiedFiles)
            {
                var path = Path.Combine(_environment.WebRootPath, "images", file);
                if (File.Exists(path)) File.Delete(path);
            }
            throw;
        }
    }
}
