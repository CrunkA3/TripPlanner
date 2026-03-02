using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaceImages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlaceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImageData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ImageContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaceImages_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaceImages_PlaceId",
                table: "PlaceImages",
                column: "PlaceId");

            // Migrate existing image data from Places to PlaceImages
            migrationBuilder.Sql(@"
                INSERT INTO PlaceImages (Id, PlaceId, ImageData, ImageContentType, SortOrder, CreatedAt)
                SELECT NEWID(), Id, ImageData, ImageContentType, 0, GETUTCDATE()
                FROM Places
                WHERE ImageData IS NOT NULL AND ImageContentType IS NOT NULL
            ");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Places");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Places",
                type: "varbinary(max)",
                nullable: true);

            // Migrate first image back to Places
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.ImageData = pi.ImageData, p.ImageContentType = pi.ImageContentType
                FROM Places p
                INNER JOIN (
                    SELECT PlaceId, ImageData, ImageContentType,
                           ROW_NUMBER() OVER (PARTITION BY PlaceId ORDER BY SortOrder, CreatedAt) AS rn
                    FROM PlaceImages
                ) pi ON pi.PlaceId = p.Id AND pi.rn = 1
            ");

            migrationBuilder.DropTable(
                name: "PlaceImages");
        }
    }
}
