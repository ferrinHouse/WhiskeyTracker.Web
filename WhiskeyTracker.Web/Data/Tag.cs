using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;

    public string? CreatedByUserId { get; set; }
    
    public List<TastingNoteTag> TastingNoteTags { get; set; } = new();
}
