using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class FixStudentIdNullableAuto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentExamPapers_Students_StudentId",
                table: "StudentExamPapers");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentExamPapers_Students_StudentId",
                table: "StudentExamPapers",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentExamPapers_Students_StudentId",
                table: "StudentExamPapers");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentExamPapers_Students_StudentId",
                table: "StudentExamPapers",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
