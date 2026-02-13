using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class AddXAndYPositionOfBarcodeToExamTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "X",
                table: "Exams",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Y",
                table: "Exams",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Title",
                table: "Exams",
                column: "Title",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_Title",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "X",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Y",
                table: "Exams");
        }
    }
}
