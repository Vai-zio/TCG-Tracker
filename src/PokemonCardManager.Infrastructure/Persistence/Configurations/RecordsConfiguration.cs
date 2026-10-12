using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokemonCardManager.Core.Records;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> setting)
    {
        setting.Property(s => s.Key).HasMaxLength(200);
        setting.HasIndex(s => s.Key).IsUnique();
    }
}

internal sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> filter)
    {
        filter.Property(f => f.Name).HasMaxLength(100).UseCollation("NOCASE");
        filter.HasIndex(f => f.Name).IsUnique();
    }
}

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> job)
    {
        job.Property(j => j.FileName).HasMaxLength(500);
        job.Property(j => j.Format).HasMaxLength(20);
        job.HasIndex(j => j.StartedAt);
    }
}

internal sealed class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> backup)
    {
        backup.Property(b => b.FilePath).HasMaxLength(1000);
        backup.Property(b => b.Sha256).HasMaxLength(64).IsFixedLength();
        backup.Property(b => b.SchemaVersion).HasMaxLength(150);
        backup.HasIndex(b => b.CreatedAt);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> entry)
    {
        entry.Property(e => e.EntityType).HasMaxLength(100);
        entry.Property(e => e.ChangedProperties).HasMaxLength(2000);
        entry.HasIndex(e => new { e.EntityType, e.EntityId });
        entry.HasIndex(e => e.Timestamp);
    }
}
