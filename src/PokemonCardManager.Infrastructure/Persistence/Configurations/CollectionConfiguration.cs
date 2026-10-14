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
        card.Property(c => c.StoragePosition).HasMaxLength(100);
        card.Property(c => c.CertificationNumber).HasMaxLength(50);
        card.Property(c => c.Notes).HasMaxLength(4000);

        card.HasIndex(c => c.CardReferenceId);
        card.HasIndex(c => c.Status);
        card.HasIndex(c => c.StorageLocationId);
        card.HasIndex(c => c.CreatedAt);
        // A cert number identifies exactly one slab at a grader.
        card.HasIndex(c => new { c.GradingCompanyId, c.CertificationNumber }).IsUnique()
            .HasFilter("\"CertificationNumber\" IS NOT NULL");

        card.HasOne(c => c.CardReference).WithMany()
            .HasForeignKey(c => c.CardReferenceId).OnDelete(DeleteBehavior.Restrict);
        // Deleting a location leaves its cards "unplaced" rather than deleting them.
        card.HasOne(c => c.StorageLocation).WithMany()
            .HasForeignKey(c => c.StorageLocationId).OnDelete(DeleteBehavior.SetNull);
        card.HasOne(c => c.GradingCompany).WithMany()
            .HasForeignKey(c => c.GradingCompanyId).OnDelete(DeleteBehavior.Restrict);

        card.ToTable(t =>
        {
            t.OptionalAmount(nameof(PhysicalCard.PurchasePrice), nameof(PhysicalCard.PurchaseCurrency));
            t.OptionalAmount(nameof(PhysicalCard.EstimatedRawValue), nameof(PhysicalCard.EstimatedRawValueCurrency));
            t.Grade(nameof(PhysicalCard.Grade));
            // A grade only means something on a company's scale.
            t.HasCheckConstraint("CK_PhysicalCards_GradeHasCompany", "\"Grade\" IS NULL OR \"GradingCompanyId\" IS NOT NULL");
        });
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

        // The same photo can't be attached to one card twice (this also serves "images for a card").
        image.HasIndex(i => new { i.PhysicalCardId, i.Sha256 }).IsUnique();
        image.HasIndex(i => i.Sha256);
        image.HasIndex(i => i.StorageKey).IsUnique();

        image.HasOne(i => i.PhysicalCard).WithMany(c => c.Images)
            .HasForeignKey(i => i.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
        // An original can't be deleted while crops or annotated copies still point at it.
        image.HasOne(i => i.OriginalImage).WithMany()
            .HasForeignKey(i => i.OriginalImageId).OnDelete(DeleteBehavior.Restrict);

        image.ToTable(t =>
        {
            t.NonNegative(nameof(CardImage.Width), nameof(CardImage.Height), nameof(CardImage.FileSizeBytes));
            t.HasCheckConstraint("CK_CardImages_Sha256", "length(\"Sha256\") = 64");
        });
    }
}
