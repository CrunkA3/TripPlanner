using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class fk_tripPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripPlaces_TripDays_TripDayId",
                table: "TripPlaces");

            migrationBuilder.AddForeignKey(
                name: "FK_TripPlaces_TripDays_TripDayId",
                table: "TripPlaces",
                column: "TripDayId",
                principalTable: "TripDays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripPlaces_TripDays_TripDayId",
                table: "TripPlaces");

            migrationBuilder.AddForeignKey(
                name: "FK_TripPlaces_TripDays_TripDayId",
                table: "TripPlaces",
                column: "TripDayId",
                principalTable: "TripDays",
                principalColumn: "Id");
        }
    }
}
