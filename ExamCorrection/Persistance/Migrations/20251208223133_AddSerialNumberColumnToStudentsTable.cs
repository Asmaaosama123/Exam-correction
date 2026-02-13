using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialNumberColumnToStudentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Students");
        }
    }
}
