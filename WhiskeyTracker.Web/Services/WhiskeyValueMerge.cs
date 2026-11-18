using WhiskeyTracker.Web.Data;

namespace WhiskeyTracker.Web.Services;

/// The whiskey text fields an admin can clean up by merging spelling variants.
public enum WhiskeyValueField
{
    Brand,
    Type,
    Region,
    Distillery
}

public record WhiskeyValueCount(string Value, int Count);

public static class WhiskeyValueMerge
{
    public static string Get(Whiskey whiskey, WhiskeyValueField field) => field switch
    {
        WhiskeyValueField.Brand => whiskey.Brand,
        WhiskeyValueField.Type => whiskey.Type,
        WhiskeyValueField.Region => whiskey.Region,
        WhiskeyValueField.Distillery => whiskey.Distillery,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    public static void Set(Whiskey whiskey, WhiskeyValueField field, string value)
    {
        switch (field)
        {
            case WhiskeyValueField.Brand: whiskey.Brand = value; break;
            case WhiskeyValueField.Type: whiskey.Type = value; break;
            case WhiskeyValueField.Region: whiskey.Region = value; break;
            case WhiskeyValueField.Distillery: whiskey.Distillery = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    /// Matches the [MaxLength] on each Whiskey property.
    public static int MaxLength(WhiskeyValueField field) => field switch
    {
        WhiskeyValueField.Brand or WhiskeyValueField.Distillery => 100,
        _ => 60
    };

    /// Groups values that look like spelling variants of each other: values that normalize to the
    /// same text ("Maker's Mark" / "Makers Mark"), or where one is the other plus extra trailing
    /// words ("New Riff" / "New Riff Distilling"). Only groups with two or more values are returned,
    /// most-used value first. These are suggestions; the admin decides what to merge.
    public static List<List<WhiskeyValueCount>> SuggestGroups(IReadOnlyList<WhiskeyValueCount> values)
    {
        var normalized = values.Select(v => WhiskeyKey.NormalizeText(v.Value)).ToArray();
        var parent = Enumerable.Range(0, values.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        for (var i = 0; i < values.Count; i++)
        {
            if (normalized[i].Length == 0) continue;
            for (var j = i + 1; j < values.Count; j++)
            {
                if (normalized[j].Length == 0) continue;
                if (IsVariant(normalized[i], normalized[j]))
                {
                    parent[Find(i)] = Find(j);
                }
            }
        }

        return Enumerable.Range(0, values.Count)
            .GroupBy(Find)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(i => values[i])
                .OrderByDescending(v => v.Count)
                .ThenBy(v => v.Value, StringComparer.OrdinalIgnoreCase)
                .ToList())
            .OrderBy(g => g[0].Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsVariant(string a, string b) =>
        a == b || b.StartsWith(a + " ", StringComparison.Ordinal) || a.StartsWith(b + " ", StringComparison.Ordinal);
}
