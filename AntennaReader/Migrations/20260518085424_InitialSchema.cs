using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntennaReader.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntennaDiagrams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AntennaName = table.Column<string>(type: "TEXT", nullable: false),
                    AntennaOwner = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntennaDiagrams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrawingCanvasSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsLogScale = table.Column<bool>(type: "INTEGER", nullable: false),
                    lowerBound = table.Column<double>(type: "REAL", nullable: false),
                    upperBound = table.Column<double>(type: "REAL", nullable: false),
                    ContourStep = table.Column<double>(type: "REAL", nullable: false),
                    CsvExportPrecision = table.Column<int>(type: "INTEGER", nullable: false),
                    PATExportPrecision = table.Column<int>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawingCanvasSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AntennaInterpolatedMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AntennaDiagramId = table.Column<int>(type: "INTEGER", nullable: false),
                    Angle = table.Column<int>(type: "INTEGER", nullable: false),
                    DbValue = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntennaInterpolatedMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntennaInterpolatedMeasurements_AntennaDiagrams_AntennaDiagramId",
                        column: x => x.AntennaDiagramId,
                        principalTable: "AntennaDiagrams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AntennaMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AntennaDiagramId = table.Column<int>(type: "INTEGER", nullable: false),
                    Angle = table.Column<int>(type: "INTEGER", nullable: false),
                    DbValue = table.Column<double>(type: "REAL", nullable: false),
                    PosX = table.Column<double>(type: "REAL", nullable: false),
                    PosY = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntennaMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntennaMeasurements_AntennaDiagrams_AntennaDiagramId",
                        column: x => x.AntennaDiagramId,
                        principalTable: "AntennaDiagrams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntennaInterpolatedMeasurements_AntennaDiagramId_Angle",
                table: "AntennaInterpolatedMeasurements",
                columns: new[] { "AntennaDiagramId", "Angle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntennaMeasurements_AntennaDiagramId_Angle",
                table: "AntennaMeasurements",
                columns: new[] { "AntennaDiagramId", "Angle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntennaInterpolatedMeasurements");

            migrationBuilder.DropTable(
                name: "AntennaMeasurements");

            migrationBuilder.DropTable(
                name: "DrawingCanvasSettings");

            migrationBuilder.DropTable(
                name: "AntennaDiagrams");
        }
    }
}
