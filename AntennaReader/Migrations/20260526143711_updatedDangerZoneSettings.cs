using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntennaReader.Migrations
{
    /// <inheritdoc />
    public partial class updatedDangerZoneSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CenterDeadzonePercent",
                table: "DrawingCanvasSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DpMaxShift",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "FaVariance",
                table: "DrawingCanvasSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "PreBlurKernelSize",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CenterDeadzonePercent",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "DpMaxShift",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "FaVariance",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "PreBlurKernelSize",
                table: "DrawingCanvasSettings");
        }
    }
}
