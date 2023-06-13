using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SNSService : IMessengerService
    {
        public string ServiceName => nameof(SNSService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                string topicArn = "";
                string messageText = "";

                IAmazonSimpleNotificationService client = new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials("", ""), RegionEndpoint.USWest2);
                var request = new PublishRequest
                {
                    TopicArn = topicArn,
                    Message = messageText,
                };

                var response = await client.PublishAsync(request, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
