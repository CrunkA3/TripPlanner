using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GpxTrackId",
                table: "Places",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatJobs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                table: "ChatJobs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UrlImportJobs_Status",
                table: "UrlImportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Places_GpxTrackId",
                table: "Places",
                column: "GpxTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatJobs_ConversationId",
                table: "ChatJobs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatJobs_Status",
                table: "ChatJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UrlImportJobs_Status",
                table: "UrlImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_Places_GpxTrackId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_ChatJobs_ConversationId",
                table: "ChatJobs");

            migrationBuilder.DropIndex(
                name: "IX_ChatJobs_Status",
                table: "ChatJobs");

            migrationBuilder.AlterColumn<string>(
                name: "GpxTrackId",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatJobs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                table: "ChatJobs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
