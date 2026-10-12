using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CardSetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_CardSets_CardSetId",
                        column: x => x.CardSetId,
                        principalTable: "CardSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Variant = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    StorageLocation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionItems_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardSetId_Number",
                table: "Cards",
                columns: new[] { "CardSetId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Name",
                table: "Cards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CardSets_Name",
                table: "CardSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_CardId",
                table: "CollectionItems",
                column: "CardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionItems");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "CardSets");
        }
    }
}
