using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddChatJobUserIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ChatJobs_UserId",
                table: "ChatJobs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatJobs_UserId",
                table: "ChatJobs");
        }
    }
}
