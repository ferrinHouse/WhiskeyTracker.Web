using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiskeyInStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InStock",
                table: "Whiskies",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InStock",
                table: "Whiskies");
        }
    }
}
