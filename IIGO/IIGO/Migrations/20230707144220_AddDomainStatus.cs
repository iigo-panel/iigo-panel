using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIGO.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DomainName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DomainExpiration = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastChecked = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "current_timestamp"),
                    RegistrarName = table.Column<string>(type: "TEXT", nullable: true),
                    NotifyChanges = table.Column<bool>(type: "INTEGER", nullable: false),
                    DomainNotificationEmail = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainStatus", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainStatus");
        }
    }
}
