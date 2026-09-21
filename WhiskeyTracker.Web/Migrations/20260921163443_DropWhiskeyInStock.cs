using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class DropWhiskeyInStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InStock",
                table: "Whiskies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InStock",
                table: "Whiskies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
