using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SMTPService : IMessengerService
    {
        private readonly ApplicationDbContext _context;

        public SMTPService(ApplicationDbContext context)
        {
            _context = context;
        }

        public string ServiceName => nameof(SMTPService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var email = new MimeMessage();

                email.From.Add(new MailboxAddress(GetSetting("SMTP_FromName", "IIGO Panel"), GetSetting("SMTP_FromAddress", "admin@example.com")));
                email.Sender = new MailboxAddress(GetSetting("SMTP_SenderName", "IIGO Panel"), GetSetting("SMTP_SenderAddress", "admin@example.com"));

                email.To.Add(new MailboxAddress("adam@mws.dev", "adam@mws.dev"));

                email.Subject = "Test Message";

                var body = new BodyBuilder()
                {
                    HtmlBody = "<p>Test Message</p>",
                    TextBody = "Test Message"
                };
                email.Body = body.ToMessageBody();

                using var smtp = new SmtpClient();

                await smtp.ConnectAsync(GetSetting("SMTP_Host"), Convert.ToInt32(GetSetting("SMTP_Port", "587")), Convert.ToBoolean(GetSetting("SMTP_UseSSL", "true")), ct);
                await smtp.AuthenticateAsync(GetSetting("SMTP_UserName", "user"), GetSetting("SMTP_Password", "password"), ct);
                await smtp.SendAsync(email, ct);
                await smtp.DisconnectAsync(true, ct);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string GetSetting(string setting, string defaultValue = "")
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting) == null)
            {
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = setting, SettingValue = defaultValue });
                _context.SaveChanges();
            }

            return _context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting)?.SettingValue ?? defaultValue;
        }
    }
}
