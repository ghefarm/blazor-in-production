using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foresight.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicFace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outcomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Feed = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StartsLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Happened = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    Feed = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    Probability = table.Column<double>(type: "REAL", nullable: false),
                    IsPremium = table.Column<bool>(type: "INTEGER", nullable: false),
                    Happened = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Round = table.Column<string>(type: "TEXT", nullable: false),
                    Voter = table.Column<string>(type: "TEXT", nullable: false),
                    Candidate = table.Column<string>(type: "TEXT", nullable: false),
                    CastUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outcomes_Feed",
                table: "Outcomes",
                column: "Feed");

            migrationBuilder.CreateIndex(
                name: "IX_TrackRecord_EventId",
                table: "TrackRecord",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackRecord_Feed",
                table: "TrackRecord",
                column: "Feed");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_Round_Voter",
                table: "Votes",
                columns: new[] { "Round", "Voter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_Voter",
                table: "Votes",
                column: "Voter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outcomes");

            migrationBuilder.DropTable(
                name: "TrackRecord");

            migrationBuilder.DropTable(
                name: "Votes");
        }
    }
}
