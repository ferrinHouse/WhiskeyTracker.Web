using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WhiskeyTracker.Web.Data;

public class SessionParticipant
{
    public int TastingSessionId { get; set; }
    public TastingSession TastingSession { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool IsDriver { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
