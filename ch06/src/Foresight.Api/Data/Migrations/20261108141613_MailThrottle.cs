using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foresight.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailThrottle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailThrottle",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailThrottle", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailThrottle");
        }
    }
}
