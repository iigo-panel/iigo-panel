using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIGO.Migrations
{
    /// <inheritdoc />
    public partial class AddWindowsUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WindowsUpdateChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsUpdateChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WindowsUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WindowsUpdateCheckId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdateId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SizeMB = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsUpdates_WindowsUpdateChecks_WindowsUpdateCheckId",
                        column: x => x.WindowsUpdateCheckId,
                        principalTable: "WindowsUpdateChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WindowsUpdates_WindowsUpdateCheckId",
                table: "WindowsUpdates",
                column: "WindowsUpdateCheckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindowsUpdates");

            migrationBuilder.DropTable(
                name: "WindowsUpdateChecks");
        }
    }
}
