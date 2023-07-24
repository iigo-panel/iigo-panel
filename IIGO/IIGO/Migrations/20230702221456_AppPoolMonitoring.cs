using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIGO.Migrations
{
    /// <inheritdoc />
    internal partial class AppPoolMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPoolMonitoring",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppPoolName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AppPoolStartTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPoolMonitoring", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPoolMonitoring");
        }
    }
}
