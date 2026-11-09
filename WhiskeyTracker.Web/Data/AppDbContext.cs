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

        // Not unique yet: existing data may hold duplicates that need merging first.
        builder.Entity<Whiskey>()
            .HasIndex(w => w.NormalizedKey);

        builder.Entity<Whiskey>()
            .HasIndex(w => w.Barcode);

        builder.Entity<Whiskey>()
            .HasIndex(w => w.Status);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampWhiskeys();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampWhiskeys();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// Keeps NormalizedKey in step with Brand/Name/Age and stamps CreatedAt on new whiskeys,
    /// so every page and service that saves a Whiskey gets duplicate detection for free.
    private void StampWhiskeys()
    {
        foreach (var entry in ChangeTracker.Entries<Whiskey>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var key = WhiskeyKey.For(entry.Entity);
            if (entry.Entity.NormalizedKey != key)
            {
                entry.Entity.NormalizedKey = key;
                if (entry.State == EntityState.Modified)
                {
                    entry.Property(w => w.NormalizedKey).IsModified = true;
                }
            }

            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == null)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
        }
    }
}