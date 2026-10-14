using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Catalog;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class CardSetConfiguration : IEntityTypeConfiguration<CardSet>
{
    public void Configure(EntityTypeBuilder<CardSet> set)
    {
        // NOCASE: "base set" and "Base Set" are the same set.
        set.Property(s => s.Name).HasMaxLength(200).UseCollation("NOCASE");
        set.Property(s => s.Code).HasMaxLength(20).UseCollation("NOCASE");
        set.Property(s => s.Series).HasMaxLength(100);
        set.HasIndex(s => s.Name).IsUnique();
        set.HasIndex(s => s.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
    }
}

internal sealed class CardReferenceConfiguration : IEntityTypeConfiguration<CardReference>
{
    public void Configure(EntityTypeBuilder<CardReference> card)
    {
        card.Property(c => c.Name).HasMaxLength(200).UseCollation("NOCASE");
        card.Property(c => c.Subject).HasMaxLength(100).UseCollation("NOCASE");
        card.Property(c => c.CollectorNumber).HasMaxLength(20).UseCollation("NOCASE");
        card.Property(c => c.Language).HasMaxLength(2);
        card.Property(c => c.Rarity).HasMaxLength(50);
        card.Property(c => c.Edition).HasMaxLength(50).UseCollation("NOCASE");
        card.Property(c => c.ImageReference).HasMaxLength(500);
        card.Property(c => c.DataSource).HasMaxLength(100);

        // One printing = set + number + language + finish + edition. The name is NOT part of identity.
        card.HasIndex(c => new { c.CardSetId, c.CollectorNumber, c.Language, c.Finish, c.Edition }).IsUnique();
        card.HasIndex(c => c.Name);
        card.HasIndex(c => c.Subject);

        card.HasOne(c => c.CardSet).WithMany(s => s.Cards)
            .HasForeignKey(c => c.CardSetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ExternalCardIdentifierConfiguration : IEntityTypeConfiguration<ExternalCardIdentifier>
{
    public void Configure(EntityTypeBuilder<ExternalCardIdentifier> id)
    {
        id.Property(i => i.Provider).HasMaxLength(100);
        id.Property(i => i.ExternalId).HasMaxLength(200);
        id.HasIndex(i => new { i.Provider, i.ExternalId }).IsUnique();

        id.HasOne(i => i.CardReference).WithMany(c => c.ExternalIds)
            .HasForeignKey(i => i.CardReferenceId).OnDelete(DeleteBehavior.Cascade);
    }
}
