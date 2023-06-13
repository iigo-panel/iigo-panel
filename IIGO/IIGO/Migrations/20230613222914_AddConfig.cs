using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIGO.Migrations
{
    /// <inheritdoc />
    public partial class AddConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettingName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigSetting", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigSetting_SettingName",
                table: "ConfigSetting",
                column: "SettingName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigSetting");
        }
    }
}
