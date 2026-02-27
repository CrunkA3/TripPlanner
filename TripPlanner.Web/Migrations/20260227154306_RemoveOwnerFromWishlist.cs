using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOwnerFromWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "UserWishlists",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                INSERT INTO UserWishlists (UserId, WishlistId, Level, SharedAt)
                SELECT OwnerId, Id, 0, GETUTCDATE()
                FROM Wishlists");


            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_AspNetUsers_OwnerId",
                table: "Wishlists");

            migrationBuilder.DropIndex(
                name: "IX_Wishlists_OwnerId",
                table: "Wishlists");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Wishlists");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Wishlists",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Wishlists",
                type: "nvarchar(450)",
                nullable: true);



            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_ApplicationUserId",
                table: "Wishlists",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_AspNetUsers_ApplicationUserId",
                table: "Wishlists",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_AspNetUsers_ApplicationUserId",
                table: "Wishlists");

            migrationBuilder.DropIndex(
                name: "IX_Wishlists_ApplicationUserId",
                table: "Wishlists");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Wishlists");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "UserWishlists");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Wishlists",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Wishlists",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_OwnerId",
                table: "Wishlists",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_AspNetUsers_OwnerId",
                table: "Wishlists",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
