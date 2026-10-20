using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIGO.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Feature = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ServiceIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_Feature_ServiceIdentifier",
                table: "NotificationSubscriptions",
                columns: new[] { "Feature", "ServiceIdentifier" },
                unique: true);

            // Move the single notification method settings into the new table. Fresh installs
            // don't have the settings yet, so they get the previous default (SMTPService).
            migrationBuilder.Sql("""
                INSERT INTO NotificationSubscriptions (Feature, ServiceIdentifier)
                SELECT 'AppPoolMonitor', COALESCE(NULLIF((SELECT SettingValue FROM ConfigSetting WHERE SettingName = 'AppPoolNotificationMethod'), ''), 'SMTPService');

                INSERT INTO NotificationSubscriptions (Feature, ServiceIdentifier)
                SELECT 'CertificateMonitor', COALESCE(NULLIF((SELECT SettingValue FROM ConfigSetting WHERE SettingName = 'CertificateNotificationMethod'), ''), 'SMTPService');

                DELETE FROM ConfigSetting WHERE SettingName IN ('AppPoolNotificationMethod', 'CertificateNotificationMethod');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the first assigned service of each feature as its single method setting
            migrationBuilder.Sql("""
                INSERT INTO ConfigSetting (SettingName, SettingValue)
                SELECT 'AppPoolNotificationMethod', COALESCE((SELECT ServiceIdentifier FROM NotificationSubscriptions WHERE Feature = 'AppPoolMonitor' ORDER BY Id LIMIT 1), 'SMTPService');

                INSERT INTO ConfigSetting (SettingName, SettingValue)
                SELECT 'CertificateNotificationMethod', COALESCE((SELECT ServiceIdentifier FROM NotificationSubscriptions WHERE Feature = 'CertificateMonitor' ORDER BY Id LIMIT 1), 'SMTPService');
                """);

            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");
        }
    }
}
