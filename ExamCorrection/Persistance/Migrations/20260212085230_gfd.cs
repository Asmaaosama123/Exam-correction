using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class gfd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnotatedImageUrl",
                table: "StudentExamPapers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionDetailsJson",
                table: "StudentExamPapers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "StudentExamPapers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnotatedImageUrl",
                table: "StudentExamPapers");

            migrationBuilder.DropColumn(
                name: "QuestionDetailsJson",
                table: "StudentExamPapers");

            migrationBuilder.DropColumn(
                name: "TotalQuestions",
                table: "StudentExamPapers");
        }
    }
}
