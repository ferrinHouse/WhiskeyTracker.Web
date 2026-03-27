using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiskeyBrand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Whiskies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"Whiskies\" SET \"Brand\" = \"Distillery\"");

            migrationBuilder.CreateIndex(
                name: "IX_TastingSessions_UserId",
                table: "TastingSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TastingNotes_UserId",
                table: "TastingNotes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TastingNotes_AspNetUsers_UserId",
                table: "TastingNotes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TastingSessions_AspNetUsers_UserId",
                table: "TastingSessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TastingNotes_AspNetUsers_UserId",
                table: "TastingNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_TastingSessions_AspNetUsers_UserId",
                table: "TastingSessions");

            migrationBuilder.DropIndex(
                name: "IX_TastingSessions_UserId",
                table: "TastingSessions");

            migrationBuilder.DropIndex(
                name: "IX_TastingNotes_UserId",
                table: "TastingNotes");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Whiskies");
        }
    }
}
