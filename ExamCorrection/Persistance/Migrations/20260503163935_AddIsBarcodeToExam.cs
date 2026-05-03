using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBarcodeToExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBarcode",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBarcode",
                table: "Exams");
        }
    }
}
