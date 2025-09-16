using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using PostmarkDotNet;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class PostmarkService(ApplicationDbContext context) : ServiceBase(context), IMessengerService
    {
        readonly ApplicationDbContext _context = context;

        public string ServiceName => "Postmark Email Relay";
        public string ServiceIdentifier => nameof(PostmarkService);

        public bool IsEmail => true;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "PM_ServerToken") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "PM_ServerToken", SettingValue = "ServerToken" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "PM_FromAddress") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "PM_FromAddress", SettingValue = "admin@example.com" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "PM_MessageStream") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "PM_MessageStream", SettingValue = "outbound" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var msg = new PostmarkMessage(GetSetting("PM_FromAddress"), "", "", "", "", messageStream: GetSetting("PM_MessageStream"));
                var client = new PostmarkClient(GetSetting("PM_ServerToken"));
                var resp = await client.SendMessageAsync(msg);
                return resp.Status == PostmarkStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}
