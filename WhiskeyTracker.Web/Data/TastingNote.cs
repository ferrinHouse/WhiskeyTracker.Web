using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class TastingNote
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public int TastingSessionId { get; set; }
    public TastingSession TastingSession { get; set; } = null!;

    public int WhiskeyId { get; set; }
    public Whiskey Whiskey { get; set; } = null!;
    
    public int? BottleId { get; set; }
    public Bottle? Bottle { get; set; }

    public int PourAmountMl { get; set; } = 60; // Default to ~2 oz

    public int OrderIndex { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } 

    [MaxLength(2000)]
    public string? Notes { get; set; } = string.Empty;

    public List<TastingNoteTag> TastingNoteTags { get; set; } = new();
}