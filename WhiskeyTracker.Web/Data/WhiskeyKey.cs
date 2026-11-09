using System.Globalization;
using System.Text;

namespace WhiskeyTracker.Web.Data;

/// Builds the normalized brand|name|age key used to detect duplicate whiskey definitions.
public static class WhiskeyKey
{
    public const int MaxLength = 300;

    public static string For(Whiskey whiskey) => For(whiskey.Brand, whiskey.Name, whiskey.Age);

    public static string For(string? brand, string? name, int? age)
    {
        var key = $"{NormalizeText(brand)}|{NormalizeText(name)}|{age?.ToString(CultureInfo.InvariantCulture) ?? ""}";
        return key.Length > MaxLength ? key[..MaxLength] : key;
    }

    /// Lowercases, strips accents and apostrophes, turns "&" into "and", and collapses every run of
    /// other punctuation or whitespace into a single space. "Maker's Mark" and "makers  mark" match.
    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Replace("&", " and ").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue; // accents
            if (c is '\'' or '’' or '‘') continue;          // apostrophes join words

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
