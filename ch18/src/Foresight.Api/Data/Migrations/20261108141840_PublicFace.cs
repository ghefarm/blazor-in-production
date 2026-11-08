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
                name: "Votes");
        }
    }
}
