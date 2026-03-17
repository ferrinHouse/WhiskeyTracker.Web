using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhiskeyTracker.Web.Data;

public enum BottleStatus
{
    Full,
    Opened,
    Empty
}

public class Bottle
{
    public int Id { get; set; }

    // --- Ownership ---
    // This tracks who ORIGINALLY PURCHASED the bottle.
    public string? UserId { get; set; }
    [ForeignKey("UserId")]
    public ApplicationUser? Purchaser { get; set; }

    // --- Collection ---
    // The bottle belongs to a collection, which determines who can see/drink it.
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    // --- The Link to the Whiskey Definition ---
    [Required]
    public int WhiskeyId { get; set; } // Foreign Key
    public Whiskey? Whiskey { get; set; } = null!; // Navigation Property

    // --- Inventory Details ---
    [DataType(DataType.Date)]
    public DateOnly? PurchaseDate { get; set; }

    [DataType(DataType.Currency)]
    public decimal? PurchasePrice { get; set; }

    [MaxLength(100)]
    public string PurchaseLocation { get; set; } = string.Empty;

    public BottleStatus Status { get; set; } = BottleStatus.Full;
    
    // Optional: You might want to track specific bottling dates for specific bottles
    public DateOnly? BottlingDate { get; set; } 

    [Range(0, 5000)]
    public int CapacityMl { get; set; } = 750; // Default to 750ml

    [Range(0, 5000)]
    public int CurrentVolumeMl { get; set; } = 750; // Default to full bottle
    public bool IsInfinityBottle { get; set; } = false;

    public List<TastingNote> TastingNotes { get; set; } = new();
}