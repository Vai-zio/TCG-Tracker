using Microsoft.EntityFrameworkCore;
using TCGTracker.Core.Models;

namespace TCGTracker.Data;

/// <summary>
/// EF Core's view of the database. Each DbSet becomes a table;
/// OnModelCreating adds the rules (lengths, indexes, relationships).
/// </summary>
public class TcgTrackerDbContext(DbContextOptions<TcgTrackerDbContext> options) : DbContext(options)
{
    public DbSet<CardSet> CardSets => Set<CardSet>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardSet>(set =>
        {
            // NOCASE makes "base set" and "Base Set" match, so we don't create duplicates.
            set.Property(s => s.Name).HasMaxLength(200).UseCollation("NOCASE");
            set.Property(s => s.Code).HasMaxLength(20);
            set.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<Card>(card =>
        {
            card.Property(c => c.Name).HasMaxLength(200).UseCollation("NOCASE");
            card.Property(c => c.Number).HasMaxLength(20).UseCollation("NOCASE");
            card.Property(c => c.Rarity).HasMaxLength(50);
            card.HasIndex(c => new { c.CardSetId, c.Number });
            card.HasIndex(c => c.Name); // fast search by name once the collection is large

            card.HasOne(c => c.CardSet)
                .WithMany(s => s.Cards)
                .HasForeignKey(c => c.CardSetId)
                .OnDelete(DeleteBehavior.Restrict); // never silently delete cards with their set
        });

        modelBuilder.Entity<CollectionItem>(item =>
        {
            item.Property(i => i.Language).HasMaxLength(50);
            item.Property(i => i.Variant).HasMaxLength(50);
            item.Property(i => i.StorageLocation).HasMaxLength(200);
            item.Property(i => i.PurchasePrice).HasPrecision(18, 2);

            item.HasOne(i => i.Card)
                .WithMany(c => c.CollectionItems)
                .HasForeignKey(i => i.CardId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
