using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntennaReader.Migrations
{
    /// <inheritdoc />
    public partial class AddDPandFA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DpEpsilon",
                table: "DrawingCanvasSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DpSyncInterval",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FourierHarmonics",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImageDarkThreshold",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImageSaturationThreshold",
                table: "DrawingCanvasSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DpEpsilon",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "DpSyncInterval",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "FourierHarmonics",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "ImageDarkThreshold",
                table: "DrawingCanvasSettings");

            migrationBuilder.DropColumn(
                name: "ImageSaturationThreshold",
                table: "DrawingCanvasSettings");
        }
    }
}
