using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class GoogleChatService : ServiceBase, IMessengerService
    {
        private ApplicationDbContext _context;
        public GoogleChatService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(GoogleChatService);

        public bool IsEmail => false;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "GChatUrl") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "GChatUrl", SettingValue = "" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                using HttpClient c = new HttpClient();

                using var sc = new StringContent(JsonSerializer.Serialize(new { text = "" }));
                using var response = await c.PostAsync(GetSetting("GChat_Url"), sc, ct);
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
