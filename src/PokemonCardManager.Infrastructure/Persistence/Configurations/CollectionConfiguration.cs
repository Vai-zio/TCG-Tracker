using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> location)
    {
        location.Property(l => l.Name).HasMaxLength(100).UseCollation("NOCASE");
        location.Property(l => l.Notes).HasMaxLength(1000);
        location.HasIndex(l => new { l.ParentId, l.Name }).IsUnique();

        // Can't delete a binder that still has pages in it.
        location.HasOne(l => l.Parent).WithMany(l => l.Children)
            .HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PhysicalCardConfiguration : IEntityTypeConfiguration<PhysicalCard>
{
    public void Configure(EntityTypeBuilder<PhysicalCard> card)
    {
        card.Property(c => c.PurchaseCurrency).HasMaxLength(3);
        card.Property(c => c.EstimatedRawValueCurrency).HasMaxLength(3);
        card.Property(c => c.AcquisitionSource).HasMaxLength(200);
        card.Property(c => c.Notes).HasMaxLength(4000);

        card.HasIndex(c => c.CardReferenceId);
        card.HasIndex(c => c.Status);
        card.HasIndex(c => c.StorageLocationId);
        card.HasIndex(c => c.CreatedAt);

        card.HasOne(c => c.CardReference).WithMany()
            .HasForeignKey(c => c.CardReferenceId).OnDelete(DeleteBehavior.Restrict);
        // Deleting a location leaves its cards "unplaced" rather than deleting them.
        card.HasOne(c => c.StorageLocation).WithMany()
            .HasForeignKey(c => c.StorageLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CardImageConfiguration : IEntityTypeConfiguration<CardImage>
{
    public void Configure(EntityTypeBuilder<CardImage> image)
    {
        image.Ignore(i => i.IsOriginal);
        image.Property(i => i.StorageKey).HasMaxLength(500);
        image.Property(i => i.Sha256).HasMaxLength(64).IsFixedLength();
        image.Property(i => i.ContentType).HasMaxLength(100);
        image.Property(i => i.DerivationDescription).HasMaxLength(500);
        image.Property(i => i.QualityNotes).HasMaxLength(1000);

        image.HasIndex(i => i.PhysicalCardId);
        image.HasIndex(i => i.Sha256);
        image.HasIndex(i => i.StorageKey).IsUnique();

        image.HasOne(i => i.PhysicalCard).WithMany(c => c.Images)
            .HasForeignKey(i => i.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
        // An original can't be deleted while crops or annotated copies still point at it.
        image.HasOne(i => i.OriginalImage).WithMany()
            .HasForeignKey(i => i.OriginalImageId).OnDelete(DeleteBehavior.Restrict);
    }
}
