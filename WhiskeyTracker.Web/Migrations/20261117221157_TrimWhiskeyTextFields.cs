using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <summary>
    /// Trims leading/trailing whitespace from existing whiskey text fields. Untrimmed values such as
    /// "New Riff Distilling " showed up as duplicate options in the library filters. AppDbContext now
    /// trims these fields on every save, so this only cleans up older rows. NormalizedKey already
    /// ignores surrounding whitespace, so it does not need recomputing.
    /// </summary>
    public partial class TrimWhiskeyTextFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Whiskies" SET
                    "Brand" = btrim("Brand"),
                    "Name" = btrim("Name"),
                    "Distillery" = btrim("Distillery"),
                    "Region" = btrim("Region"),
                    "Type" = btrim("Type"),
                    "CaskType" = btrim("CaskType")
                WHERE "Brand" <> btrim("Brand")
                   OR "Name" <> btrim("Name")
                   OR "Distillery" <> btrim("Distillery")
                   OR "Region" <> btrim("Region")
                   OR "Type" <> btrim("Type")
                   OR "CaskType" <> btrim("CaskType");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cleanup only: the original whitespace is not recoverable and was never meaningful.
        }
    }
}
