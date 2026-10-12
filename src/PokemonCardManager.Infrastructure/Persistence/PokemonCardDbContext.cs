using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Transactions;
using PokemonCardManager.Infrastructure.Persistence.Conversions;

namespace PokemonCardManager.Infrastructure.Persistence;

/// <summary>
/// EF Core's map between C# classes and SQLite tables. Each DbSet is a table.
/// Table details (indexes, keys, delete rules) live in Configurations/, one file per area.
/// </summary>
public class PokemonCardDbContext(DbContextOptions<PokemonCardDbContext> options) : DbContext(options)
{
    public DbSet<CardSet> CardSets => Set<CardSet>();
    public DbSet<CardReference> CardReferences => Set<CardReference>();
    public DbSet<ExternalCardIdentifier> ExternalCardIdentifiers => Set<ExternalCardIdentifier>();

    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<PhysicalCard> PhysicalCards => Set<PhysicalCard>();
    public DbSet<CardImage> CardImages => Set<CardImage>();

    public DbSet<ConditionAssessment> ConditionAssessments => Set<ConditionAssessment>();
    public DbSet<CenteringMeasurement> CenteringMeasurements => Set<CenteringMeasurement>();
    public DbSet<CategoryFinding> CategoryFindings => Set<CategoryFinding>();
    public DbSet<Defect> Defects => Set<Defect>();

    public DbSet<GradingCompany> GradingCompanies => Set<GradingCompany>();
    public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<CardTransaction> Transactions => Set<CardTransaction>();
    public DbSet<TransactionLine> TransactionLines => Set<TransactionLine>();

    public DbSet<GradingSubmission> GradingSubmissions => Set<GradingSubmission>();
    public DbSet<SubmissionItem> SubmissionItems => Set<SubmissionItem>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Rules that apply to every property of a type, so we don't repeat them per column.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveConversion<FourDecimalConverter>();
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        // Enums are stored by name ("NearMint"), so reordering an enum never corrupts data.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PokemonCardDbContext).Assembly);
}
