namespace IIGO.Services
{
    /// <summary>
    /// Monitors send email through one service, chosen on the Settings page, rather than picking an email service each.
    /// </summary>
    internal static class EmailNotifications
    {
        // NotificationSubscription.ServiceIdentifier meaning "send by email". Stored, so it must not change.
        public const string ServiceIdentifier = "Email";

        // ConfigSetting holding the chosen email service's IMessengerService.ServiceIdentifier
        public const string Setting = "EmailService";

        public const string DefaultService = nameof(SMTPService);
    }
}
