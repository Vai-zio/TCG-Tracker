using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Assessment;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class ConditionAssessmentConfiguration : IEntityTypeConfiguration<ConditionAssessment>
{
    public void Configure(EntityTypeBuilder<ConditionAssessment> assessment)
    {
        assessment.Property(a => a.AssessorId).HasMaxLength(100);
        assessment.Property(a => a.AssessmentVersion).HasMaxLength(50);
        assessment.Property(a => a.UncertaintyNotes).HasMaxLength(4000);
        assessment.Property(a => a.Reasoning).HasMaxLength(8000);

        assessment.HasIndex(a => new { a.PhysicalCardId, a.AssessedAt });

        // History is never deleted along with a card.
        assessment.HasOne(a => a.PhysicalCard).WithMany()
            .HasForeignKey(a => a.PhysicalCardId).OnDelete(DeleteBehavior.Restrict);
        assessment.HasOne(a => a.GradingCompany).WithMany()
            .HasForeignKey(a => a.GradingCompanyId).OnDelete(DeleteBehavior.Restrict);

        assessment.HasMany(a => a.CenteringMeasurements).WithOne()
            .HasForeignKey(m => m.ConditionAssessmentId).OnDelete(DeleteBehavior.Cascade);
        assessment.HasMany(a => a.Findings).WithOne()
            .HasForeignKey(f => f.ConditionAssessmentId).OnDelete(DeleteBehavior.Cascade);
        assessment.HasMany(a => a.Defects).WithOne()
            .HasForeignKey(d => d.ConditionAssessmentId).OnDelete(DeleteBehavior.Cascade);

        assessment.ToTable(t => t.HasCheckConstraint(
            "CK_ConditionAssessments_GradeRange",
            "\"EstimatedGradeLow\" IS NULL OR \"EstimatedGradeHigh\" IS NULL OR \"EstimatedGradeLow\" <= \"EstimatedGradeHigh\""));
    }
}

internal sealed class CenteringMeasurementConfiguration : IEntityTypeConfiguration<CenteringMeasurement>
{
    public void Configure(EntityTypeBuilder<CenteringMeasurement> measurement)
    {
        measurement.Property(m => m.Unit).HasMaxLength(10);
        measurement.Property(m => m.Method).HasMaxLength(500);
        measurement.HasOne(m => m.CardImage).WithMany()
            .HasForeignKey(m => m.CardImageId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CategoryFindingConfiguration : IEntityTypeConfiguration<CategoryFinding>
{
    public void Configure(EntityTypeBuilder<CategoryFinding> finding)
    {
        finding.Property(f => f.Summary).HasMaxLength(4000);
        finding.HasIndex(f => new { f.ConditionAssessmentId, f.Category }).IsUnique();
        finding.ToTable(t => t.HasCheckConstraint("CK_CategoryFindings_Severity", "\"Severity\" BETWEEN 0 AND 5"));
    }
}

internal sealed class DefectConfiguration : IEntityTypeConfiguration<Defect>
{
    public void Configure(EntityTypeBuilder<Defect> defect)
    {
        defect.Property(d => d.Notes).HasMaxLength(4000);
        defect.HasIndex(d => d.ConditionAssessmentId);
        defect.HasOne(d => d.CardImage).WithMany()
            .HasForeignKey(d => d.CardImageId).OnDelete(DeleteBehavior.Restrict);
        defect.ToTable(t => t.HasCheckConstraint("CK_Defects_Severity", "\"Severity\" BETWEEN 0 AND 5"));
    }
}
