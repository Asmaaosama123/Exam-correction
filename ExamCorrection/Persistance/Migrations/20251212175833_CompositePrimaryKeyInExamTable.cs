using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class CompositePrimaryKeyInExamTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_Title",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Title_OwnerId",
                table: "Exams",
                columns: new[] { "Title", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_Title_OwnerId",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Title",
                table: "Exams",
                column: "Title",
                unique: true);
        }
    }
}
