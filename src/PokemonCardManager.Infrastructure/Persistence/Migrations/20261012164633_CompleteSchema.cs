using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PokemonCardManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceObservations_CardReferenceId_ObservedAt",
                table: "PriceObservations");

            migrationBuilder.DropIndex(
                name: "IX_GradingSubmissions_GradingCompanyId",
                table: "GradingSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_CardImages_PhysicalCardId",
                table: "CardImages");

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "TransactionLines",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "GradingFee",
                table: "SubmissionItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PredictedGradeHigh",
                table: "SubmissionItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PredictedGradeLow",
                table: "SubmissionItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawCondition",
                table: "PriceObservations",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificationNumber",
                table: "PhysicalCards",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Grade",
                table: "PhysicalCards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GradingCompanyId",
                table: "PhysicalCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePosition",
                table: "PhysicalCards",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceivedByGraderOn",
                table: "GradingSubmissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnedOn",
                table: "GradingSubmissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceTierId",
                table: "GradingSubmissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEncrypted",
                table: "BackupRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "BackupRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GradingServiceTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GradingCompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FeePerCard = table.Column<long>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    MaxDeclaredValue = table.Column<long>(type: "INTEGER", nullable: true),
                    TurnaroundDays = table.Column<int>(type: "INTEGER", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingServiceTiers", x => x.Id);
                    table.CheckConstraint("CK__Currency", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK__FeePerCard", "\"FeePerCard\" IS NULL OR \"FeePerCard\" >= 0");
                    table.CheckConstraint("CK__MaxDeclaredValue", "\"MaxDeclaredValue\" IS NULL OR \"MaxDeclaredValue\" >= 0");
                    table.CheckConstraint("CK__TurnaroundDays", "\"TurnaroundDays\" IS NULL OR \"TurnaroundDays\" >= 0");
                    table.ForeignKey(
                        name: "FK_GradingServiceTiers_GradingCompanies_GradingCompanyId",
                        column: x => x.GradingCompanyId,
                        principalTable: "GradingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GradingCompanies",
                columns: new[] { "Id", "Code", "Name", "Website" },
                values: new object[,]
                {
                    { new Guid("0199d6a0-0000-7000-8000-000000000004"), "SGC", "Sportscard Guaranty", "https://www.gosgc.com" },
                    { new Guid("0199d6a0-0000-7000-8000-000000000005"), "TAG", "TAG Grading", "https://taggrading.com" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK__Amount",
                table: "Transactions",
                sql: "\"Amount\" IS NULL OR \"Amount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Currency",
                table: "Transactions",
                sql: "length(\"Currency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Fees",
                table: "Transactions",
                sql: "\"Fees\" IS NULL OR \"Fees\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__ShippingCost",
                table: "Transactions",
                sql: "\"ShippingCost\" IS NULL OR \"ShippingCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__AllocatedAmount",
                table: "TransactionLines",
                sql: "\"AllocatedAmount\" IS NULL OR \"AllocatedAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionItems_CertificationNumber",
                table: "SubmissionItems",
                column: "CertificationNumber");

            migrationBuilder.AddCheckConstraint(
                name: "CK__DeclaredValue",
                table: "SubmissionItems",
                sql: "\"DeclaredValue\" IS NULL OR \"DeclaredValue\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__GradingFee",
                table: "SubmissionItems",
                sql: "\"GradingFee\" IS NULL OR \"GradingFee\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__PredictedGradeHigh",
                table: "SubmissionItems",
                sql: "\"PredictedGradeHigh\" IS NULL OR (\"PredictedGradeHigh\" >= 10000 AND \"PredictedGradeHigh\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__PredictedGradeLow",
                table: "SubmissionItems",
                sql: "\"PredictedGradeLow\" IS NULL OR (\"PredictedGradeLow\" >= 10000 AND \"PredictedGradeLow\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__ReturnedGrade",
                table: "SubmissionItems",
                sql: "\"ReturnedGrade\" IS NULL OR (\"ReturnedGrade\" >= 10000 AND \"ReturnedGrade\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubmissionItems_PredictedRange",
                table: "SubmissionItems",
                sql: "\"PredictedGradeLow\" IS NULL OR \"PredictedGradeHigh\" IS NULL OR \"PredictedGradeLow\" <= \"PredictedGradeHigh\"");

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_CardReferenceId_GradingCompanyId_Grade_ObservedAt",
                table: "PriceObservations",
                columns: new[] { "CardReferenceId", "GradingCompanyId", "Grade", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_ObservationType",
                table: "PriceObservations",
                column: "ObservationType");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Currency",
                table: "PriceObservations",
                sql: "length(\"Currency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Grade",
                table: "PriceObservations",
                sql: "\"Grade\" IS NULL OR (\"Grade\" >= 10000 AND \"Grade\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Price",
                table: "PriceObservations",
                sql: "\"Price\" IS NULL OR \"Price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceObservations_RawConditionOnlyWhenRaw",
                table: "PriceObservations",
                sql: "\"RawCondition\" IS NULL OR \"Grade\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCards_GradingCompanyId_CertificationNumber",
                table: "PhysicalCards",
                columns: new[] { "GradingCompanyId", "CertificationNumber" },
                unique: true,
                filter: "\"CertificationNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK__EstimatedRawValue",
                table: "PhysicalCards",
                sql: "\"EstimatedRawValue\" IS NULL OR \"EstimatedRawValue\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__EstimatedRawValue_HasCurrency",
                table: "PhysicalCards",
                sql: "\"EstimatedRawValue\" IS NULL OR \"EstimatedRawValueCurrency\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK__EstimatedRawValueCurrency",
                table: "PhysicalCards",
                sql: "\"EstimatedRawValueCurrency\" IS NULL OR length(\"EstimatedRawValueCurrency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Grade",
                table: "PhysicalCards",
                sql: "\"Grade\" IS NULL OR (\"Grade\" >= 10000 AND \"Grade\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__PurchaseCurrency",
                table: "PhysicalCards",
                sql: "\"PurchaseCurrency\" IS NULL OR length(\"PurchaseCurrency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__PurchasePrice",
                table: "PhysicalCards",
                sql: "\"PurchasePrice\" IS NULL OR \"PurchasePrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__PurchasePrice_HasCurrency",
                table: "PhysicalCards",
                sql: "\"PurchasePrice\" IS NULL OR \"PurchaseCurrency\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCards_GradeHasCompany",
                table: "PhysicalCards",
                sql: "\"Grade\" IS NULL OR \"GradingCompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GradingSubmissions_GradingCompanyId_ExternalReference",
                table: "GradingSubmissions",
                columns: new[] { "GradingCompanyId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GradingSubmissions_ServiceTierId",
                table: "GradingSubmissions",
                column: "ServiceTierId");

            migrationBuilder.AddCheckConstraint(
                name: "CK__ActualTotalCost",
                table: "GradingSubmissions",
                sql: "\"ActualTotalCost\" IS NULL OR \"ActualTotalCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Currency",
                table: "GradingSubmissions",
                sql: "length(\"Currency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__GradingFees",
                table: "GradingSubmissions",
                sql: "\"GradingFees\" IS NULL OR \"GradingFees\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__InsuranceCost",
                table: "GradingSubmissions",
                sql: "\"InsuranceCost\" IS NULL OR \"InsuranceCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__ShippingCost",
                table: "GradingSubmissions",
                sql: "\"ShippingCost\" IS NULL OR \"ShippingCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__FromCurrency",
                table: "ExchangeRates",
                sql: "length(\"FromCurrency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK__ToCurrency",
                table: "ExchangeRates",
                sql: "length(\"ToCurrency\") = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExchangeRates_Rate",
                table: "ExchangeRates",
                sql: "\"Rate\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__BoxHeight",
                table: "Defects",
                sql: "\"BoxHeight\" IS NULL OR (\"BoxHeight\" >= 0 AND \"BoxHeight\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__BoxWidth",
                table: "Defects",
                sql: "\"BoxWidth\" IS NULL OR (\"BoxWidth\" >= 0 AND \"BoxWidth\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__BoxX",
                table: "Defects",
                sql: "\"BoxX\" IS NULL OR (\"BoxX\" >= 0 AND \"BoxX\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__BoxY",
                table: "Defects",
                sql: "\"BoxY\" IS NULL OR (\"BoxY\" >= 0 AND \"BoxY\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Confidence",
                table: "Defects",
                sql: "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Defects_BoxComplete",
                table: "Defects",
                sql: "(\"BoxX\" IS NULL) = (\"BoxY\" IS NULL) AND (\"BoxX\" IS NULL) = (\"BoxWidth\" IS NULL) AND (\"BoxX\" IS NULL) = (\"BoxHeight\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Defects_BoxInsideImage",
                table: "Defects",
                sql: "\"BoxX\" IS NULL OR (\"BoxX\" + \"BoxWidth\" <= 1 AND \"BoxY\" + \"BoxHeight\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Confidence",
                table: "ConditionAssessments",
                sql: "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__EstimatedGradeHigh",
                table: "ConditionAssessments",
                sql: "\"EstimatedGradeHigh\" IS NULL OR (\"EstimatedGradeHigh\" >= 10000 AND \"EstimatedGradeHigh\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__EstimatedGradeLow",
                table: "ConditionAssessments",
                sql: "\"EstimatedGradeLow\" IS NULL OR (\"EstimatedGradeLow\" >= 10000 AND \"EstimatedGradeLow\" <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConditionAssessments_GradeHasCompany",
                table: "ConditionAssessments",
                sql: "(\"EstimatedGradeLow\" IS NULL AND \"EstimatedGradeHigh\" IS NULL) OR \"GradingCompanyId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK__BottomBorder",
                table: "CenteringMeasurements",
                sql: "\"BottomBorder\" IS NULL OR \"BottomBorder\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Confidence",
                table: "CenteringMeasurements",
                sql: "\"Confidence\" IS NULL OR (\"Confidence\" >= 0 AND \"Confidence\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK__LeftBorder",
                table: "CenteringMeasurements",
                sql: "\"LeftBorder\" IS NULL OR \"LeftBorder\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__RightBorder",
                table: "CenteringMeasurements",
                sql: "\"RightBorder\" IS NULL OR \"RightBorder\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__TopBorder",
                table: "CenteringMeasurements",
                sql: "\"TopBorder\" IS NULL OR \"TopBorder\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_PhysicalCardId_Sha256",
                table: "CardImages",
                columns: new[] { "PhysicalCardId", "Sha256" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK__FileSizeBytes",
                table: "CardImages",
                sql: "\"FileSizeBytes\" IS NULL OR \"FileSizeBytes\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Height",
                table: "CardImages",
                sql: "\"Height\" IS NULL OR \"Height\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK__Width",
                table: "CardImages",
                sql: "\"Width\" IS NULL OR \"Width\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CardImages_Sha256",
                table: "CardImages",
                sql: "length(\"Sha256\") = 64");

            migrationBuilder.CreateIndex(
                name: "IX_GradingServiceTiers_GradingCompanyId_Name_EffectiveFrom",
                table: "GradingServiceTiers",
                columns: new[] { "GradingCompanyId", "Name", "EffectiveFrom" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GradingSubmissions_GradingServiceTiers_ServiceTierId",
                table: "GradingSubmissions",
                column: "ServiceTierId",
                principalTable: "GradingServiceTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCards_GradingCompanies_GradingCompanyId",
                table: "PhysicalCards",
                column: "GradingCompanyId",
                principalTable: "GradingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GradingSubmissions_GradingServiceTiers_ServiceTierId",
                table: "GradingSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCards_GradingCompanies_GradingCompanyId",
                table: "PhysicalCards");

            migrationBuilder.DropTable(
                name: "GradingServiceTiers");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Amount",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Currency",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Fees",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__ShippingCost",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__AllocatedAmount",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionItems_CertificationNumber",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK__DeclaredValue",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK__GradingFee",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK__PredictedGradeHigh",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK__PredictedGradeLow",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK__ReturnedGrade",
                table: "SubmissionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubmissionItems_PredictedRange",
                table: "SubmissionItems");

            migrationBuilder.DropIndex(
                name: "IX_PriceObservations_CardReferenceId_GradingCompanyId_Grade_ObservedAt",
                table: "PriceObservations");

            migrationBuilder.DropIndex(
                name: "IX_PriceObservations_ObservationType",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Currency",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Grade",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Price",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceObservations_RawConditionOnlyWhenRaw",
                table: "PriceObservations");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCards_GradingCompanyId_CertificationNumber",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__EstimatedRawValue",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__EstimatedRawValue_HasCurrency",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__EstimatedRawValueCurrency",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Grade",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__PurchaseCurrency",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__PurchasePrice",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK__PurchasePrice_HasCurrency",
                table: "PhysicalCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCards_GradeHasCompany",
                table: "PhysicalCards");

            migrationBuilder.DropIndex(
                name: "IX_GradingSubmissions_GradingCompanyId_ExternalReference",
                table: "GradingSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_GradingSubmissions_ServiceTierId",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__ActualTotalCost",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Currency",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__GradingFees",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__InsuranceCost",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__ShippingCost",
                table: "GradingSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK__FromCurrency",
                table: "ExchangeRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK__ToCurrency",
                table: "ExchangeRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExchangeRates_Rate",
                table: "ExchangeRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK__BoxHeight",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK__BoxWidth",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK__BoxX",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK__BoxY",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Confidence",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Defects_BoxComplete",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Defects_BoxInsideImage",
                table: "Defects");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Confidence",
                table: "ConditionAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK__EstimatedGradeHigh",
                table: "ConditionAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK__EstimatedGradeLow",
                table: "ConditionAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConditionAssessments_GradeHasCompany",
                table: "ConditionAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK__BottomBorder",
                table: "CenteringMeasurements");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Confidence",
                table: "CenteringMeasurements");

            migrationBuilder.DropCheckConstraint(
                name: "CK__LeftBorder",
                table: "CenteringMeasurements");

            migrationBuilder.DropCheckConstraint(
                name: "CK__RightBorder",
                table: "CenteringMeasurements");

            migrationBuilder.DropCheckConstraint(
                name: "CK__TopBorder",
                table: "CenteringMeasurements");

            migrationBuilder.DropIndex(
                name: "IX_CardImages_PhysicalCardId_Sha256",
                table: "CardImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK__FileSizeBytes",
                table: "CardImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Height",
                table: "CardImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK__Width",
                table: "CardImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CardImages_Sha256",
                table: "CardImages");

            migrationBuilder.DeleteData(
                table: "GradingCompanies",
                keyColumn: "Id",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "GradingCompanies",
                keyColumn: "Id",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000005"));

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "GradingFee",
                table: "SubmissionItems");

            migrationBuilder.DropColumn(
                name: "PredictedGradeHigh",
                table: "SubmissionItems");

            migrationBuilder.DropColumn(
                name: "PredictedGradeLow",
                table: "SubmissionItems");

            migrationBuilder.DropColumn(
                name: "RawCondition",
                table: "PriceObservations");

            migrationBuilder.DropColumn(
                name: "CertificationNumber",
                table: "PhysicalCards");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "PhysicalCards");

            migrationBuilder.DropColumn(
                name: "GradingCompanyId",
                table: "PhysicalCards");

            migrationBuilder.DropColumn(
                name: "StoragePosition",
                table: "PhysicalCards");

            migrationBuilder.DropColumn(
                name: "ReceivedByGraderOn",
                table: "GradingSubmissions");

            migrationBuilder.DropColumn(
                name: "ReturnedOn",
                table: "GradingSubmissions");

            migrationBuilder.DropColumn(
                name: "ServiceTierId",
                table: "GradingSubmissions");

            migrationBuilder.DropColumn(
                name: "IsEncrypted",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "BackupRecords");

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_CardReferenceId_ObservedAt",
                table: "PriceObservations",
                columns: new[] { "CardReferenceId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GradingSubmissions_GradingCompanyId",
                table: "GradingSubmissions",
                column: "GradingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CardImages_PhysicalCardId",
                table: "CardImages",
                column: "PhysicalCardId");
        }
    }
}
