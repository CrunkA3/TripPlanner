using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaceCollections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PublicShareToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaceCollections_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaceCollectionItems",
                columns: table => new
                {
                    CollectionId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PlaceId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceCollectionItems", x => new { x.CollectionId, x.PlaceId });
                    table.ForeignKey(
                        name: "FK_PlaceCollectionItems_PlaceCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "PlaceCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaceCollectionItems_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaceCollectionItems_PlaceId",
                table: "PlaceCollectionItems",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceCollections_OwnerId",
                table: "PlaceCollections",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaceCollections_PublicShareToken",
                table: "PlaceCollections",
                column: "PublicShareToken",
                unique: true,
                filter: "[PublicShareToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaceCollectionItems");

            migrationBuilder.DropTable(
                name: "PlaceCollections");
        }
    }
}
