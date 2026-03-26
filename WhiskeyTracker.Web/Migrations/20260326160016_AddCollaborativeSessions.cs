using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WhiskeyTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborativeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "TastingSessions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionLineupItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TastingSessionId = table.Column<int>(type: "integer", nullable: false),
                    WhiskeyId = table.Column<int>(type: "integer", nullable: false),
                    BottleId = table.Column<int>(type: "integer", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionLineupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionLineupItems_Bottles_BottleId",
                        column: x => x.BottleId,
                        principalTable: "Bottles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionLineupItems_TastingSessions_TastingSessionId",
                        column: x => x.TastingSessionId,
                        principalTable: "TastingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionLineupItems_Whiskies_WhiskeyId",
                        column: x => x.WhiskeyId,
                        principalTable: "Whiskies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionParticipants",
                columns: table => new
                {
                    TastingSessionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsDriver = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionParticipants", x => new { x.TastingSessionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SessionParticipants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionParticipants_TastingSessions_TastingSessionId",
                        column: x => x.TastingSessionId,
                        principalTable: "TastingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionLineupItems_BottleId",
                table: "SessionLineupItems",
                column: "BottleId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionLineupItems_TastingSessionId",
                table: "SessionLineupItems",
                column: "TastingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionLineupItems_WhiskeyId",
                table: "SessionLineupItems",
                column: "WhiskeyId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_UserId",
                table: "SessionParticipants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionLineupItems");

            migrationBuilder.DropTable(
                name: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "TastingSessions");
        }
    }
}
