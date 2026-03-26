using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WhiskeyTracker.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Whiskey> Whiskies { get; set; }
    public DbSet<Bottle> Bottles { get; set; }
    public DbSet<TastingSession> TastingSessions { get; set; }
    public DbSet<TastingNote> TastingNotes { get; set; }
    public DbSet<BlendComponent> BlendComponents { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<CollectionMember> CollectionMembers { get; set; }
    public DbSet<CollectionInvitation> CollectionInvitations { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TastingNoteTag> TastingNoteTags { get; set; }
    public DbSet<SessionParticipant> SessionParticipants { get; set; }
    public DbSet<SessionLineupItem> SessionLineupItems { get; set; }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TastingNoteTag>()
            .HasKey(tnt => new { tnt.TastingNoteId, tnt.TagId, tnt.Field });

        // SessionParticipant composite key
        builder.Entity<SessionParticipant>()
            .HasKey(sp => new { sp.TastingSessionId, sp.UserId });

        builder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();
    }
}