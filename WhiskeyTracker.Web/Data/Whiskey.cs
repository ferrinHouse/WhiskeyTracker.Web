//WhiskeyTracker.Web/Data/Whiskey.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WhiskeyTracker.Web.Data;

/// Where a whiskey definition came from.
public enum WhiskeySource
{
    Manual = 0,
    PhotoScan = 1,
    Import = 2,
    Mcp = 3
}

/// Review state of a whiskey definition. Only Approved whiskeys appear in the library and pickers;
/// machine-sourced records start as Pending until an admin reviews them.
public enum WhiskeyStatus
{
    Approved = 0,
    Pending = 1,
    Rejected = 2
}

public class Whiskey
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Rare Cask"

    [Required]
    [MaxLength(100)]
    public string Brand { get; set; } = string.Empty; // e.g., "Macallan"

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

    // The fields below are set by the app (import service, SaveChanges), never bound from forms.
    [MaxLength(32)]
    [BindNever]
    public string? Barcode { get; set; }

    [BindNever]
    public WhiskeySource Source { get; set; } = WhiskeySource.Manual;

    [MaxLength(200)]
    [BindNever]
    public string? SourceRef { get; set; }

    [BindNever]
    public WhiskeyStatus Status { get; set; } = WhiskeyStatus.Approved;

    /// Duplicate-detection key (brand|name|age, normalized). Kept current by AppDbContext.SaveChanges.
    [MaxLength(300)]
    [BindNever]
    [ValidateNever]
    public string NormalizedKey { get; set; } = string.Empty;

    /// Null for whiskeys created before this column existed.
    [BindNever]
    public DateTime? CreatedAt { get; set; }

    [ValidateNever]
    public List<Bottle> Bottles { get; set; } = new();
    
    [ValidateNever]
    public List<TastingNote> TastingNotes { get; set; } = new();

    // We'll add complex fields like Ratings, Notes, and Images in later slices!
}