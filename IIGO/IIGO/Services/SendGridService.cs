using IIGO.Models;
using IIGO.Services.Interfaces;
using SendGrid.Helpers.Mail;
using SendGrid;
using System;
using System.Threading;
using System.Threading.Tasks;
using IIGO.Data;
using System.Linq;

namespace IIGO.Services
{
    public class SendGridService : ServiceBase, IMessengerService
    {
        private ApplicationDbContext _context;
        public SendGridService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(SendGridService);

        public bool IsEmail => true;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SG_ApiKey") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SG_ApiKey", SettingValue = "SendGridApiKey" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SG_FromName") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SG_FromName", SettingValue = "IIGO Panel" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SG_FromAddress") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SG_FromAddress", SettingValue = "admin@example.com" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var client = new SendGridClient(GetSetting("SG_ApiKey"));
                var from = new EmailAddress(GetSetting("SG_FromAddress"), GetSetting("SG_FromName"));
                var to = new EmailAddress("", "");
                var msg = MailHelper.CreateSingleEmail(from, to, "", "", "");
                var response = await client.SendEmailAsync(msg, ct);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }
    }
}
