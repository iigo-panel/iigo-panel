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
    internal class DiscordService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        // Discord rejects messages longer than this
        const int MaxLength = 2000;

        public string ServiceName => "Discord";
        public string ServiceIdentifier => nameof(DiscordService);

        public bool IsEmail => false;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("Discord_WebhookUrl", "Webhook URL", MonitorSettingType.Secret, "", "From the channel's Integrations settings in Discord."),
            new("Discord_UserName", "Post as", MonitorSettingType.Text, "IIGOPanel", "The name messages appear under.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            var text = $"**{message.Subject}**\n{message.Body}";
            if (text.Length > MaxLength)
                text = text[..(MaxLength - 1)] + "…";

            using var client = new DiscordWebhookClient(GetRequired("Discord_WebhookUrl", secret: true));
            var userName = GetSetting("Discord_UserName");
            await client.SendMessageAsync(text, username: string.IsNullOrWhiteSpace(userName) ? null : userName);
            return true;
        }
    }
}
