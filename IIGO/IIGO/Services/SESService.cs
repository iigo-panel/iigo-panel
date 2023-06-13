using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SESService : IMessengerService
    {
        public string ServiceName => nameof(SESService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            Message msg = new Message(new Content(""), new Body() { Html = new Content("") });
            SendEmailRequest req = new SendEmailRequest("", new Destination(new List<string> { "" }), msg) { ReturnPath = "" };
            //if (!String.IsNullOrWhiteSpace(replyTo))
            //    req.ReplyToAddresses.Add(replyTo);
            using (AmazonSimpleEmailServiceClient client = new AmazonSimpleEmailServiceClient(new BasicAWSCredentials("", ""), RegionEndpoint.USWest2))
            {
                try
                {
                    var resp = await client.SendEmailAsync(req, ct);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
