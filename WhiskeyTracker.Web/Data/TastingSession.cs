using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class TastingSession
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? JoinCode { get; set; }

    public List<TastingNote> Notes { get; set; } = new();
    public List<SessionParticipant> Participants { get; set; } = new();
    public List<SessionLineupItem> Lineup { get; set; } = new();
}