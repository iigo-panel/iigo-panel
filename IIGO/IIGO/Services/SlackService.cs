using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using Slack.Webhooks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SlackService : ServiceBase, IMessengerService
    {
        private readonly ApplicationDbContext _context;
        public SlackService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(SlackService);

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "Slack_URL") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "Slack_URL", SettingValue = "" });

            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            var slackClient = new SlackClient(GetSetting("Slack_URL"));
            var slackMessage = new SlackMessage
            {
                Channel = "",
                Text = "",
                IconEmoji = Emoji.Computer,
                Username = ""
            };
            try
            {
                return await Task.Run(() => slackClient.Post(slackMessage));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
