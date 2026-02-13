using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCorrection.Migrations
{
    /// <inheritdoc />
    public partial class AddRTeacherRoleInsteadOfMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "01988dc9-8542-7feb-bc91-64a7aa6ab9e2",
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "Teacher", "TEACHER" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "01988aa7-16d7-73c0-ab67-75ebb23fab2b",
                column: "PhoneNumber",
                value: "966532410900");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "01988dc9-8542-7feb-bc91-64a7aa6ab9e2",
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "Member", "MEMBER" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "01988aa7-16d7-73c0-ab67-75ebb23fab2b",
                column: "PhoneNumber",
                value: null);
        }
    }
}
