using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    public partial class AddStudentExamPaperColumnsSafely : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FinalScore
            migrationBuilder.AddColumn<float>(
                name: "FinalScore",
                table: "StudentExamPapers",
                type: "real",
                nullable: true);

            // TotalQuestions
            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "StudentExamPapers",
                type: "int",
                nullable: true,
                defaultValue: 0);

            // QuestionDetailsJson
            migrationBuilder.AddColumn<string>(
                name: "QuestionDetailsJson",
                table: "StudentExamPapers",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "{}");

            // AnnotatedImageUrl
            migrationBuilder.AddColumn<string>(
                name: "AnnotatedImageUrl",
                table: "StudentExamPapers",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FinalScore", table: "StudentExamPapers");
            migrationBuilder.DropColumn(name: "TotalQuestions", table: "StudentExamPapers");
            migrationBuilder.DropColumn(name: "QuestionDetailsJson", table: "StudentExamPapers");
            migrationBuilder.DropColumn(name: "AnnotatedImageUrl", table: "StudentExamPapers");
        }
    }
}
