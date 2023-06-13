using IIGO.Models;
using IIGO.Services.Interfaces;
using PostmarkDotNet;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class PostmarkService : IMessengerService
    {
        public string ServiceName => nameof(PostmarkService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var msg = new PostmarkMessage("", "", "", "", "");
                var client = new PostmarkClient("");
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
