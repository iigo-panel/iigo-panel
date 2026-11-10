using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class SMTPService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "SMTP Email Relay";
        public string ServiceIdentifier => nameof(SMTPService);

        public bool IsEmail => true;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("SMTP_Host", "Server", MonitorSettingType.Text, "smtp.example.com"),
            new("SMTP_Port", "Port", MonitorSettingType.Number, "587", "Usually 587, or 465 for TLS from the start of the connection."),
            new("SMTP_UseSSL", "Use TLS", MonitorSettingType.Toggle, "true", "Port 465 connects with TLS; any other port upgrades with STARTTLS."),
            new("SMTP_UserName", "User name", MonitorSettingType.Text, "", "Leave blank if the server doesn't need a sign-in."),
            new("SMTP_Password", "Password", MonitorSettingType.Secret, ""),
            new("SMTP_FromName", "From name", MonitorSettingType.Text, "IIGO Panel"),
            new("SMTP_FromAddress", "From address", MonitorSettingType.Email, "admin@example.com"),
            new("SMTP_SenderName", "Sender name", MonitorSettingType.Text, "", "Optional. Only needed when the server sends on behalf of the from address."),
            new("SMTP_SenderAddress", "Sender address", MonitorSettingType.Email, "")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            RequireRecipients(message);

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(GetSetting("SMTP_FromName"), GetRequired("SMTP_FromAddress")));

            var senderAddress = GetSetting("SMTP_SenderAddress");
            if (!string.IsNullOrWhiteSpace(senderAddress))
                email.Sender = new MailboxAddress(GetSetting("SMTP_SenderName"), senderAddress);

            foreach (var recipient in message.Recipients)
                email.To.Add(MailboxAddress.Parse(recipient));

            email.Subject = message.Subject;
            email.Body = new BodyBuilder
            {
                HtmlBody = message.HtmlBody,
                TextBody = message.Body
            }.ToMessageBody();

            var port = int.TryParse(GetSetting("SMTP_Port", "587"), out var p) ? p : 587;
            var security = !bool.TryParse(GetSetting("SMTP_UseSSL", "true"), out var useTls) || useTls
                ? (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(GetRequired("SMTP_Host"), port, security, ct);

            var userName = GetSetting("SMTP_UserName");
            if (!string.IsNullOrWhiteSpace(userName))
                await smtp.AuthenticateAsync(userName, GetSecret("SMTP_Password"), ct);

            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);

            return true;
        }
    }
}
