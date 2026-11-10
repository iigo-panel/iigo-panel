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
    internal class SlackService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "Slack";
        public string ServiceIdentifier => nameof(SlackService);

        public bool IsEmail => false;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("Slack_URL", "Webhook URL", MonitorSettingType.Secret, "", "An incoming webhook URL from your Slack app."),
            new("Slack_Channel", "Channel", MonitorSettingType.Text, "", "Optional. Leave blank to post to the webhook's own channel."),
            new("Slack_UserName", "Post as", MonitorSettingType.Text, "IIGO Panel", "Optional. Newer Slack apps ignore this and use the app's name.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            var slackClient = new SlackClient(GetRequired("Slack_URL", secret: true));
            var slackMessage = new SlackMessage
            {
                Text = $"*{message.Subject}*\n{message.Body}",
                IconEmoji = Emoji.Computer
            };

            var channel = GetSetting("Slack_Channel");
            if (!string.IsNullOrWhiteSpace(channel))
                slackMessage.Channel = channel;
            var userName = GetSetting("Slack_UserName");
            if (!string.IsNullOrWhiteSpace(userName))
                slackMessage.Username = userName;

            return await slackClient.PostAsync(slackMessage);
        }
    }
}
