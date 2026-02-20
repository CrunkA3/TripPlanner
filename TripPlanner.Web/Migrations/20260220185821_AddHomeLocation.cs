using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HomeLatitude",
                table: "AspNetUsers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeLocationName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLongitude",
                table: "AspNetUsers",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeLatitude",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HomeLocationName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HomeLongitude",
                table: "AspNetUsers");
        }
    }
}
