using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class SessionLineupItem
{
    public int Id { get; set; }

    public int TastingSessionId { get; set; }
    public TastingSession TastingSession { get; set; } = null!;

    public int WhiskeyId { get; set; }
    public Whiskey Whiskey { get; set; } = null!;

    public int? BottleId { get; set; }
    public Bottle? Bottle { get; set; }

    public int OrderIndex { get; set; }
}
