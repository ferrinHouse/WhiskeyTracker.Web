using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public enum TastingField
{
    Nose,
    Palate,
    Finish
}

public class TastingNoteTag
{
    public int TastingNoteId { get; set; }
    public TastingNote TastingNote { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public TastingField Field { get; set; }
}
