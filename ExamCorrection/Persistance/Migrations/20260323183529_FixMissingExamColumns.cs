using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingExamColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameMarkData",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FiducialsData",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameMarkData",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "FiducialsData",
                table: "Exams");
        }
    }
}
