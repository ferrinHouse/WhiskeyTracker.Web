using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace WhiskeyTracker.Web.Data;

public static class WhiskeyStockQueries
{
    public static readonly Expression<Func<Bottle, bool>> IsUsable =
        b => b.Status != BottleStatus.Empty && b.CurrentVolumeMl > 0;

    /// The single definition of "in stock": the whiskey has a usable bottle in one of the given collections.
    public static async Task<HashSet<int>> GetInStockWhiskeyIdsAsync(
        this AppDbContext context, IReadOnlyCollection<int> collectionIds)
    {
        if (collectionIds.Count == 0) return new HashSet<int>();

        var ids = await context.Bottles
            .Where(IsUsable)
            .Where(b => b.CollectionId.HasValue && collectionIds.Contains(b.CollectionId.Value))
            .Select(b => b.WhiskeyId)
            .Distinct()
            .ToListAsync();

        return ids.ToHashSet();
    }
}
