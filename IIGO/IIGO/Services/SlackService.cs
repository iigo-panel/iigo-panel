using IIGO.Models;
using IIGO.Services.Interfaces;
using Slack.Webhooks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SlackService : IMessengerService
    {
        public string ServiceName => nameof(SlackService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            var slackClient = new SlackClient("");
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
