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
    public class SMTPService : ServiceBase, IMessengerService
    {
        private readonly ApplicationDbContext _context;

        public SMTPService(ApplicationDbContext context) : base(context)
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

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_FromName") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_FromName", SettingValue = "IIGO Panel" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_FromAddress") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_FromAddress", SettingValue = "admin@example.com" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_SenderName") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_SenderName", SettingValue = "IIGO Panel" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_SenderAddress") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_SenderAddress", SettingValue = "admin@example.com" });

            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_Host") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_Host", SettingValue = "smtp.example.com" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_Port") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_Port", SettingValue = "587" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_UseSSL") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_UseSSL", SettingValue = "true" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_UserName") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_UserName", SettingValue = "user" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SMTP_Password") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SMTP_Password", SettingValue = "password" });

            _context.SaveChanges();
        }
    }
}
