namespace WhiskeyTracker.Web.Data;

public static class WhiskeyQueryExtensions
{
    /// Whiskeys that are live in the library. Pending (awaiting review) and Rejected whiskeys are
    /// excluded; use this for every user-facing list, search and picker.
    public static IQueryable<Whiskey> Approved(this IQueryable<Whiskey> whiskies) =>
        whiskies.Where(w => w.Status == WhiskeyStatus.Approved);
}
