using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiskeyImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Whiskies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Whiskies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedKey",
                table: "Whiskies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Whiskies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceRef",
                table: "Whiskies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Whiskies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Whiskies_Barcode",
                table: "Whiskies",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Whiskies_NormalizedKey",
                table: "Whiskies",
                column: "NormalizedKey");

            migrationBuilder.CreateIndex(
                name: "IX_Whiskies_Status",
                table: "Whiskies",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Whiskies_Barcode",
                table: "Whiskies");

            migrationBuilder.DropIndex(
                name: "IX_Whiskies_NormalizedKey",
                table: "Whiskies");

            migrationBuilder.DropIndex(
                name: "IX_Whiskies_Status",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "NormalizedKey",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "SourceRef",
                table: "Whiskies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Whiskies");
        }
    }
}
