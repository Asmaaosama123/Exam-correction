using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class MakeNameAndOwnerIdIsComositeKeyInClassTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_Name",
                table: "Classes");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Name_OwnerId",
                table: "Classes",
                columns: new[] { "Name", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_Name_OwnerId",
                table: "Classes");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Name",
                table: "Classes",
                column: "Name",
                unique: true);
        }
    }
}
