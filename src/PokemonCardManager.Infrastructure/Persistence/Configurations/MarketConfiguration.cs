using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Infrastructure.Persistence.Conversions;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class GradingCompanyConfiguration : IEntityTypeConfiguration<GradingCompany>
{
    // Fixed ids so the seed data is identical in every database.
    public static readonly Guid PsaId = new("0199d6a0-0000-7000-8000-000000000001");
    public static readonly Guid BgsId = new("0199d6a0-0000-7000-8000-000000000002");
    public static readonly Guid CgcId = new("0199d6a0-0000-7000-8000-000000000003");

    public void Configure(EntityTypeBuilder<GradingCompany> company)
    {
        company.Property(c => c.Code).HasMaxLength(10).UseCollation("NOCASE");
        company.Property(c => c.Name).HasMaxLength(100);
        company.Property(c => c.Website).HasMaxLength(200);
        company.HasIndex(c => c.Code).IsUnique();

        company.HasData(
            new GradingCompany { Id = PsaId, Code = "PSA", Name = "Professional Sports Authenticator", Website = "https://www.psacard.com" },
            new GradingCompany { Id = BgsId, Code = "BGS", Name = "Beckett Grading Services", Website = "https://www.beckett.com/grading" },
            new GradingCompany { Id = CgcId, Code = "CGC", Name = "CGC Cards", Website = "https://www.cgccards.com" });
    }
}

internal sealed class PriceObservationConfiguration : IEntityTypeConfiguration<PriceObservation>
{
    public void Configure(EntityTypeBuilder<PriceObservation> price)
    {
        price.Property(p => p.Currency).HasMaxLength(3);
        price.Property(p => p.Source).HasMaxLength(100);
        price.Property(p => p.SourceReference).HasMaxLength(500);
        price.Property(p => p.Notes).HasMaxLength(1000);

        price.HasIndex(p => new { p.CardReferenceId, p.ObservedAt });

        price.HasOne(p => p.CardReference).WithMany()
            .HasForeignKey(p => p.CardReferenceId).OnDelete(DeleteBehavior.Cascade);
        price.HasOne(p => p.GradingCompany).WithMany()
            .HasForeignKey(p => p.GradingCompanyId).OnDelete(DeleteBehavior.Restrict);

        // A graded price needs both company and grade; a raw price has neither.
        price.ToTable(t => t.HasCheckConstraint(
            "CK_PriceObservations_GradedIdentity",
            "(\"GradingCompanyId\" IS NULL) = (\"Grade\" IS NULL)"));
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> rate)
    {
        rate.Property(r => r.FromCurrency).HasMaxLength(3);
        rate.Property(r => r.ToCurrency).HasMaxLength(3);
        rate.Property(r => r.Source).HasMaxLength(100);
        rate.Property(r => r.Rate).HasConversion<EightDecimalConverter>();
        rate.HasIndex(r => new { r.FromCurrency, r.ToCurrency, r.AsOf, r.Source }).IsUnique();
    }
}
