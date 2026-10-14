using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PokemonCardManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ChangedProperties = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IncludesImages = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, collation: "NOCASE"),
                    Series = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    TotalCards = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Rate = table.Column<long>(type: "INTEGER", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradingCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorReport = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    FilterJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageLocations_StorageLocations_ParentId",
                        column: x => x.ParentId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    Fees = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingCost = table.Column<long>(type: "INTEGER", nullable: false),
                    Counterparty = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    CardSetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CollectorNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    Language = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Finish = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsPromo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Edition = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    ImageReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DataSource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardReferences_CardSets_CardSetId",
                        column: x => x.CardSetId,
                        principalTable: "CardSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GradingCompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ServiceTier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SubmittedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    GradingFees = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingCost = table.Column<long>(type: "INTEGER", nullable: false),
                    InsuranceCost = table.Column<long>(type: "INTEGER", nullable: false),
                    ActualTotalCost = table.Column<long>(type: "INTEGER", nullable: true),
                    OutboundTracking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReturnTracking = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingSubmissions_GradingCompanies_GradingCompanyId",
                        column: x => x.GradingCompanyId,
                        principalTable: "GradingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalCardIdentifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCardIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCardIdentifiers_CardReferences_CardReferenceId",
                        column: x => x.CardReferenceId,
                        principalTable: "CardReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcquiredOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PurchasePrice = table.Column<long>(type: "INTEGER", nullable: true),
                    PurchaseCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    AcquisitionSource = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StorageLocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RawCondition = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EstimatedRawValue = table.Column<long>(type: "INTEGER", nullable: true),
                    EstimatedRawValueCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalCards_CardReferences_CardReferenceId",
                        column: x => x.CardReferenceId,
                        principalTable: "CardReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCards_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PriceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GradingCompanyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Grade = table.Column<long>(type: "INTEGER", nullable: true),
                    Price = table.Column<long>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ObservationType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SaleDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceObservations", x => x.Id);
                    table.CheckConstraint("CK_PriceObservations_GradedIdentity", "(\"GradingCompanyId\" IS NULL) = (\"Grade\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PriceObservations_CardReferences_CardReferenceId",
                        column: x => x.CardReferenceId,
                        principalTable: "CardReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceObservations_GradingCompanies_GradingCompanyId",
                        column: x => x.GradingCompanyId,
                        principalTable: "GradingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhysicalCardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DerivationDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SharpnessScore = table.Column<double>(type: "REAL", nullable: true),
                    MeanBrightness = table.Column<double>(type: "REAL", nullable: true),
                    QualityNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardImages_CardImages_OriginalImageId",
                        column: x => x.OriginalImageId,
                        principalTable: "CardImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardImages_PhysicalCards_PhysicalCardId",
                        column: x => x.PhysicalCardId,
                        principalTable: "PhysicalCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConditionAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhysicalCardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AssessorId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssessmentVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImageQuality = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    GradingCompanyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EstimatedGradeLow = table.Column<long>(type: "INTEGER", nullable: true),
                    EstimatedGradeHigh = table.Column<long>(type: "INTEGER", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    UncertaintyNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    InsufficientEvidence = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Reasoning = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionAssessments", x => x.Id);
                    table.CheckConstraint("CK_ConditionAssessments_GradeRange", "\"EstimatedGradeLow\" IS NULL OR \"EstimatedGradeHigh\" IS NULL OR \"EstimatedGradeLow\" <= \"EstimatedGradeHigh\"");
                    table.ForeignKey(
                        name: "FK_ConditionAssessments_GradingCompanies_GradingCompanyId",
                        column: x => x.GradingCompanyId,
                        principalTable: "GradingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConditionAssessments_PhysicalCards_PhysicalCardId",
                        column: x => x.PhysicalCardId,
                        principalTable: "PhysicalCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhysicalCardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllocatedAmount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionLines_PhysicalCards_PhysicalCardId",
                        column: x => x.PhysicalCardId,
                        principalTable: "PhysicalCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionLines_Transactions_CardTransactionId",
                        column: x => x.CardTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConditionAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryFindings", x => x.Id);
                    table.CheckConstraint("CK_CategoryFindings_Severity", "\"Severity\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_CategoryFindings_ConditionAssessments_ConditionAssessmentId",
                        column: x => x.ConditionAssessmentId,
                        principalTable: "ConditionAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CenteringMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConditionAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LeftBorder = table.Column<double>(type: "REAL", nullable: false),
                    RightBorder = table.Column<double>(type: "REAL", nullable: false),
                    TopBorder = table.Column<double>(type: "REAL", nullable: false),
                    BottomBorder = table.Column<double>(type: "REAL", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CardImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Method = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CenteringMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CenteringMeasurements_CardImages_CardImageId",
                        column: x => x.CardImageId,
                        principalTable: "CardImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CenteringMeasurements_ConditionAssessments_ConditionAssessmentId",
                        column: x => x.ConditionAssessmentId,
                        principalTable: "ConditionAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Defects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConditionAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CardImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BoxX = table.Column<double>(type: "REAL", nullable: true),
                    BoxY = table.Column<double>(type: "REAL", nullable: true),
                    BoxWidth = table.Column<double>(type: "REAL", nullable: true),
                    BoxHeight = table.Column<double>(type: "REAL", nullable: true),
                    DetectionMethod = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Defects", x => x.Id);
                    table.CheckConstraint("CK_Defects_Severity", "\"Severity\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_Defects_CardImages_CardImageId",
                        column: x => x.CardImageId,
                        principalTable: "CardImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Defects_ConditionAssessments_ConditionAssessmentId",
                        column: x => x.ConditionAssessmentId,
                        principalTable: "ConditionAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GradingSubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhysicalCardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeclaredValue = table.Column<long>(type: "INTEGER", nullable: true),
                    ReturnedGrade = table.Column<long>(type: "INTEGER", nullable: true),
                    CertificationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionItems_ConditionAssessments_PredictionAssessmentId",
                        column: x => x.PredictionAssessmentId,
                        principalTable: "ConditionAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionItems_GradingSubmissions_GradingSubmissionId",
                        column: x => x.GradingSubmissionId,
                        principalTable: "GradingSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionItems_PhysicalCards_PhysicalCardId",
                        column: x => x.PhysicalCardId,
                        principalTable: "PhysicalCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GradingCompanies",
                columns: new[] { "Id", "Code", "Name", "Website" },
                values: new object[,]
                {
                    { new Guid("0199d6a0-0000-7000-8000-000000000001"), "PSA", "Professional Sports Authenticator", "https://www.psacard.com" },
                    { new Guid("0199d6a0-0000-7000-8000-000000000002"), "BGS", "Beckett Grading Services", "https://www.beckett.com/grading" },
                    { new Guid("0199d6a0-0000-7000-8000-000000000003"), "CGC", "CGC Cards", "https://www.cgccards.com" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Timestamp",
                table: "AuditEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_CreatedAt",
                table: "BackupRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_OriginalImageId",
                table: "CardImages",
                column: "OriginalImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_PhysicalCardId",
                table: "CardImages",
                column: "PhysicalCardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_Sha256",
                table: "CardImages",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_StorageKey",
                table: "CardImages",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardReferences_CardSetId_CollectorNumber_Language_Finish_Edition",
                table: "CardReferences",
                columns: new[] { "CardSetId", "CollectorNumber", "Language", "Finish", "Edition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardReferences_Name",
                table: "CardReferences",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CardReferences_Subject",
                table: "CardReferences",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_CardSets_Code",
                table: "CardSets",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardSets_Name",
                table: "CardSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFindings_ConditionAssessmentId_Category",
                table: "CategoryFindings",
                columns: new[] { "ConditionAssessmentId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CenteringMeasurements_CardImageId",
                table: "CenteringMeasurements",
                column: "CardImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CenteringMeasurements_ConditionAssessmentId",
                table: "CenteringMeasurements",
                column: "ConditionAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionAssessments_GradingCompanyId",
                table: "ConditionAssessments",
                column: "GradingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionAssessments_PhysicalCardId_AssessedAt",
                table: "ConditionAssessments",
                columns: new[] { "PhysicalCardId", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Defects_CardImageId",
                table: "Defects",
                column: "CardImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Defects_ConditionAssessmentId",
                table: "Defects",
                column: "ConditionAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_FromCurrency_ToCurrency_AsOf_Source",
                table: "ExchangeRates",
                columns: new[] { "FromCurrency", "ToCurrency", "AsOf", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCardIdentifiers_CardReferenceId",
                table: "ExternalCardIdentifiers",
                column: "CardReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCardIdentifiers_Provider_ExternalId",
                table: "ExternalCardIdentifiers",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingCompanies_Code",
                table: "GradingCompanies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingSubmissions_GradingCompanyId",
                table: "GradingSubmissions",
                column: "GradingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingSubmissions_Status",
                table: "GradingSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_StartedAt",
                table: "ImportJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCards_CardReferenceId",
                table: "PhysicalCards",
                column: "CardReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCards_CreatedAt",
                table: "PhysicalCards",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCards_Status",
                table: "PhysicalCards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCards_StorageLocationId",
                table: "PhysicalCards",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_CardReferenceId_ObservedAt",
                table: "PriceObservations",
                columns: new[] { "CardReferenceId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_GradingCompanyId",
                table: "PriceObservations",
                column: "GradingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_Name",
                table: "SavedFilters",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_ParentId_Name",
                table: "StorageLocations",
                columns: new[] { "ParentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionItems_GradingSubmissionId_PhysicalCardId",
                table: "SubmissionItems",
                columns: new[] { "GradingSubmissionId", "PhysicalCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionItems_PhysicalCardId",
                table: "SubmissionItems",
                column: "PhysicalCardId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionItems_PredictionAssessmentId",
                table: "SubmissionItems",
                column: "PredictionAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_CardTransactionId_PhysicalCardId",
                table: "TransactionLines",
                columns: new[] { "CardTransactionId", "PhysicalCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_PhysicalCardId",
                table: "TransactionLines",
                column: "PhysicalCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "CategoryFindings");

            migrationBuilder.DropTable(
                name: "CenteringMeasurements");

            migrationBuilder.DropTable(
                name: "Defects");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "ExternalCardIdentifiers");

            migrationBuilder.DropTable(
                name: "ImportJobs");

            migrationBuilder.DropTable(
                name: "PriceObservations");

            migrationBuilder.DropTable(
                name: "SavedFilters");

            migrationBuilder.DropTable(
                name: "SubmissionItems");

            migrationBuilder.DropTable(
                name: "TransactionLines");

            migrationBuilder.DropTable(
                name: "CardImages");

            migrationBuilder.DropTable(
                name: "ConditionAssessments");

            migrationBuilder.DropTable(
                name: "GradingSubmissions");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "PhysicalCards");

            migrationBuilder.DropTable(
                name: "GradingCompanies");

            migrationBuilder.DropTable(
                name: "CardReferences");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropTable(
                name: "CardSets");
        }
    }
}
