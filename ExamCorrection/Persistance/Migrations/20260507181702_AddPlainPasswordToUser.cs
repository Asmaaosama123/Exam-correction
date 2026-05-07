using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddPlainPasswordToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlainPassword",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "01988aa7-16d7-73c0-ab67-75ebb23fab2b",
                column: "PlainPassword",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlainPassword",
                table: "AspNetUsers");
        }
    }
}
