using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class TripAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TripId",
                table: "Places",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_TripId",
                table: "Places",
                column: "TripId");

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Trips_TripId",
                table: "Places",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Places_Trips_TripId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_TripId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Places");
        }
    }
}
