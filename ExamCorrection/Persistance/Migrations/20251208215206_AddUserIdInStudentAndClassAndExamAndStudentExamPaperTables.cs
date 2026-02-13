using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdInStudentAndClassAndExamAndStudentExamPaperTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Students",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "StudentExamPapers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Exams",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Classes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Classes",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Students_OwnerId",
                table: "Students",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentExamPapers_OwnerId",
                table: "StudentExamPapers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OwnerId",
                table: "Exams",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OwnerId",
                table: "Classes",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_AspNetUsers_OwnerId",
                table: "Classes",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_AspNetUsers_OwnerId",
                table: "Exams",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentExamPapers_AspNetUsers_OwnerId",
                table: "StudentExamPapers",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AspNetUsers_OwnerId",
                table: "Students",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_AspNetUsers_OwnerId",
                table: "Classes");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_AspNetUsers_OwnerId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentExamPapers_AspNetUsers_OwnerId",
                table: "StudentExamPapers");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_AspNetUsers_OwnerId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_OwnerId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_StudentExamPapers_OwnerId",
                table: "StudentExamPapers");

            migrationBuilder.DropIndex(
                name: "IX_Exams_OwnerId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Classes_OwnerId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "StudentExamPapers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Classes");
        }
    }
}
