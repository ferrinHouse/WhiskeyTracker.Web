using System.Globalization;
using System.Text;
using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Services;

/// Builds a CSV export of the bottles in a collection.
public static class CollectionCsvExporter
{
    private static readonly string[] Header =
    {
        "Name", "Brand", "Distillery", "Region", "Type", "Age", "ABV", "CaskType",
        "Status", "CapacityMl", "CurrentVolumeMl", "PurchaseDate", "PurchasePrice",
        "PurchaseLocation", "BottlingDate", "InfinityBottle"
    };

    public static string Build(IEnumerable<Bottle> bottles)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Header)).Append("\r\n");

        foreach (var b in bottles)
        {
            var w = b.Whiskey;
            var fields = new[]
            {
                w?.Name, w?.Brand, w?.Distillery, w?.Region, w?.Type,
                w?.Age?.ToString(CultureInfo.InvariantCulture),
                w?.ABV?.ToString(CultureInfo.InvariantCulture),
                w?.CaskType,
                b.Status.ToString(),
                b.CapacityMl.ToString(CultureInfo.InvariantCulture),
                b.CurrentVolumeMl.ToString(CultureInfo.InvariantCulture),
                b.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                b.PurchasePrice?.ToString(CultureInfo.InvariantCulture),
                b.PurchaseLocation,
                b.BottlingDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                b.IsInfinityBottle ? "true" : "false"
            };
            sb.Append(string.Join(',', fields.Select(Escape))).Append("\r\n");
        }

        return sb.ToString();
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        // Neutralize spreadsheet formula injection (=, +, -, @, tab, CR).
        if ("=+-@\t\r".Contains(value[0])) value = "'" + value;

        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
