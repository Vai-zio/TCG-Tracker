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
        transaction.ToTable(t =>
        {
            t.Currency(nameof(CardTransaction.Currency));
            t.NonNegative(nameof(CardTransaction.Amount), nameof(CardTransaction.Fees), nameof(CardTransaction.ShippingCost));
        });

        transaction.HasMany(t => t.Lines).WithOne(l => l.CardTransaction)
            .HasForeignKey(l => l.CardTransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLine>
{
    public void Configure(EntityTypeBuilder<TransactionLine> line)
    {
        line.HasIndex(l => new { l.CardTransactionId, l.PhysicalCardId }).IsUnique();
        line.HasIndex(l => l.PhysicalCardId); // "every purchase/sale of this card"
        line.ToTable(t => t.NonNegative(nameof(TransactionLine.AllocatedAmount)));
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
        submission.HasIndex(s => new { s.GradingCompanyId, s.ExternalReference }).IsUnique()
            .HasFilter("\"ExternalReference\" IS NOT NULL");
        submission.HasOne(s => s.ServiceTierDetails).WithMany()
            .HasForeignKey(s => s.ServiceTierId).OnDelete(DeleteBehavior.Restrict);
        submission.ToTable(t =>
        {
            t.Currency(nameof(GradingSubmission.Currency));
            t.NonNegative(nameof(GradingSubmission.GradingFees), nameof(GradingSubmission.ShippingCost),
                nameof(GradingSubmission.InsuranceCost), nameof(GradingSubmission.ActualTotalCost));
        });

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
        item.HasIndex(i => i.CertificationNumber);
        item.ToTable(t =>
        {
            t.NonNegative(nameof(SubmissionItem.DeclaredValue), nameof(SubmissionItem.GradingFee));
            t.Grade(nameof(SubmissionItem.PredictedGradeLow), nameof(SubmissionItem.PredictedGradeHigh),
                nameof(SubmissionItem.ReturnedGrade));
            t.HasCheckConstraint("CK_SubmissionItems_PredictedRange",
                "\"PredictedGradeLow\" IS NULL OR \"PredictedGradeHigh\" IS NULL OR \"PredictedGradeLow\" <= \"PredictedGradeHigh\"");
        });

        item.HasOne(i => i.PhysicalCard).WithMany()
            .HasForeignKey(i => i.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(i => i.PredictionAssessment).WithMany()
            .HasForeignKey(i => i.PredictionAssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
