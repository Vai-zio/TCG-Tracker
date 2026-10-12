using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Transactions;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class CardTransactionConfiguration : IEntityTypeConfiguration<CardTransaction>
{
    public void Configure(EntityTypeBuilder<CardTransaction> transaction)
    {
        transaction.Property(t => t.Currency).HasMaxLength(3);
        transaction.Property(t => t.Counterparty).HasMaxLength(200);
        transaction.Property(t => t.Platform).HasMaxLength(100);
        transaction.Property(t => t.Notes).HasMaxLength(4000);
        transaction.HasIndex(t => t.Date);

        transaction.HasMany(t => t.Lines).WithOne(l => l.CardTransaction)
            .HasForeignKey(l => l.CardTransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLine>
{
    public void Configure(EntityTypeBuilder<TransactionLine> line)
    {
        line.HasIndex(l => new { l.CardTransactionId, l.PhysicalCardId }).IsUnique();
        line.HasOne(l => l.PhysicalCard).WithMany()
            .HasForeignKey(l => l.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GradingSubmissionConfiguration : IEntityTypeConfiguration<GradingSubmission>
{
    public void Configure(EntityTypeBuilder<GradingSubmission> submission)
    {
        submission.Property(s => s.ExternalReference).HasMaxLength(100);
        submission.Property(s => s.ServiceTier).HasMaxLength(100);
        submission.Property(s => s.Currency).HasMaxLength(3);
        submission.Property(s => s.OutboundTracking).HasMaxLength(100);
        submission.Property(s => s.ReturnTracking).HasMaxLength(100);
        submission.Property(s => s.Notes).HasMaxLength(4000);
        submission.HasIndex(s => s.Status);

        submission.HasOne(s => s.GradingCompany).WithMany()
            .HasForeignKey(s => s.GradingCompanyId).OnDelete(DeleteBehavior.Restrict);
        submission.HasMany(s => s.Items).WithOne(i => i.GradingSubmission)
            .HasForeignKey(i => i.GradingSubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SubmissionItemConfiguration : IEntityTypeConfiguration<SubmissionItem>
{
    public void Configure(EntityTypeBuilder<SubmissionItem> item)
    {
        item.Property(i => i.CertificationNumber).HasMaxLength(50);
        item.Property(i => i.Notes).HasMaxLength(1000);
        // A card can't be in the same submission twice.
        item.HasIndex(i => new { i.GradingSubmissionId, i.PhysicalCardId }).IsUnique();
        item.HasIndex(i => i.PhysicalCardId);

        item.HasOne(i => i.PhysicalCard).WithMany()
            .HasForeignKey(i => i.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(i => i.PredictionAssessment).WithMany()
            .HasForeignKey(i => i.PredictionAssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
