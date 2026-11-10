using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class GoogleChatService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "Google Chat";
        public string ServiceIdentifier => nameof(GoogleChatService);

        public bool IsEmail => false;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("GChat_Url", "Webhook URL", MonitorSettingType.Secret, "", "From the space's Apps & integrations settings in Google Chat.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            using HttpClient c = new HttpClient();
            using var sc = new StringContent(JsonSerializer.Serialize(new { text = $"*{message.Subject}*\n{message.Body}" }), Encoding.UTF8, "application/json");
            using var response = await c.PostAsync(GetRequired("GChat_Url", secret: true), sc, ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Google Chat returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

            return true;
        }
    }
}
