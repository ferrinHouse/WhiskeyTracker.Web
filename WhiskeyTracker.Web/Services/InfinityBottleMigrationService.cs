using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Services;

/// Gives infinity bottles that share a Whiskey record with another bottle their own Whiskey,
/// copying only the bottle image from the original.
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
    }

    /// Infinity bottles whose Whiskey is also used by at least one other bottle.
    public async Task<List<SharedInfinityBottle>> FindSharedAsync()
    {
        return await _context.Bottles
            .Where(b => b.IsInfinityBottle &&
                        _context.Bottles.Any(o => o.WhiskeyId == b.WhiskeyId && o.Id != b.Id))
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

    /// Returns the number of infinity bottles that were given their own Whiskey.
    public async Task<int> MigrateAsync()
    {
        var shared = await FindSharedAsync();
        if (shared.Count == 0) return 0;

        var bottleIds = shared.Select(s => s.BottleId).ToList();
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
                    Name = source.Name,
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
