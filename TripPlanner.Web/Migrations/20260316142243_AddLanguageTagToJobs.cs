using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageTagToJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "UrlImportJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "ChatJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "UrlImportJobs");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "ChatJobs");
        }
    }
}
