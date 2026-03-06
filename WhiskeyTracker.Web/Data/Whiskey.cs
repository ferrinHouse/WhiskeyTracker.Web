//WhiskeyTracker.Web/Data/Whiskey.cs
using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class Whiskey
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Rare Cask"

    [Required]
    [MaxLength(100)]
    public string Distillery { get; set; } = string.Empty; // e.g., "Macallan"

    [MaxLength(60)]
    public string Region { get; set; } = string.Empty; // e.g., "Speyside"

    [MaxLength(60)]
    public string Type { get; set; } = string.Empty; // e.g., "Single Malt", "Bourbon"

    [Display(Name = "Age (Years)")]
    public int? Age { get; set; }

    [Display(Name = "ABV %")]
    [Range(0, 100)]
    public double? ABV { get; set; }

    [MaxLength(100)]
    [Display(Name = "Cask Type")]
    public string? CaskType { get; set; }

    public string? ImageFileName { get; set; }

    [MaxLength(2000)]
    public string? GeneralNotes { get; set; }

    public List<Bottle> Bottles { get; set; } = new();
    public List<TastingNote> TastingNotes { get; set; } = new();

    // We'll add complex fields like Ratings, Notes, and Images in later slices!
}