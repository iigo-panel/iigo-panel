using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class GoogleChatService : IMessengerService
    {
        public string ServiceName => nameof(GoogleChatService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                using HttpClient c = new HttpClient();
                c.BaseAddress = new Uri("");

                using var sc = new StringContent(JsonSerializer.Serialize(new { text = "" }));
                using var response = await c.PostAsync("", sc, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync();
                    throw new Exception(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
