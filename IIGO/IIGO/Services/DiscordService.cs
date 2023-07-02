using Discord.Webhook;
using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class DiscordService : ServiceBase, IMessengerService
    {
        ApplicationDbContext _context;
        public DiscordService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(DiscordService);

        public bool IsEmail => false;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "Discord_WebhookUrl") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "Discord_WebhookUrl", SettingValue = "" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "Discord_UserName") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "Discord_UserName", SettingValue = "IIGOPanel" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var c = new DiscordWebhookClient(GetSetting("Discord_WebhookUrl"));
                await c.SendMessageAsync("", username: GetSetting("Discord_UserName"), avatarUrl: "");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
