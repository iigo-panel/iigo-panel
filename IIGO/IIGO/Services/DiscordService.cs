using Discord.Webhook;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class DiscordService : IMessengerService
    {
        public string ServiceName => nameof(DiscordService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var c = new DiscordWebhookClient("");
                await c.SendMessageAsync("", username: "", avatarUrl: "");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
