using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace IIGO.Services
{
    internal class SendGridService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "SendGrid Email Relay";
        public string ServiceIdentifier => nameof(SendGridService);

        public bool IsEmail => true;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("SG_ApiKey", "API key", MonitorSettingType.Secret, "", "Needs the Mail Send permission."),
            new("SG_FromName", "From name", MonitorSettingType.Text, "IIGO Panel"),
            new("SG_FromAddress", "From address", MonitorSettingType.Email, "admin@example.com", "Must be a verified sender in SendGrid.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            RequireRecipients(message);

            var client = new SendGridClient(GetRequired("SG_ApiKey", secret: true));
            var from = new EmailAddress(GetRequired("SG_FromAddress"), GetSetting("SG_FromName"));
            var to = message.Recipients.Select(x => new EmailAddress(x)).ToList();
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, to, message.Subject, message.Body, message.HtmlBody);

            var response = await client.SendEmailAsync(msg, ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"SendGrid returned {(int)response.StatusCode}: {await response.Body.ReadAsStringAsync(ct)}");

            return true;
        }
    }
}
