using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using PostmarkDotNet;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class PostmarkService : ServiceBase, IMessengerService
    {
        private ApplicationDbContext _context;
        public PostmarkService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(PostmarkService);

        public bool IsEmail => true;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "PM_ServerToken") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "PM_ServerToken", SettingValue = "ServerToken" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "PM_FromAddress") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "PM_FromAddress", SettingValue = "admin@example.com" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var msg = new PostmarkMessage(GetSetting("PM_FromAddress"), "", "", "", "");
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
