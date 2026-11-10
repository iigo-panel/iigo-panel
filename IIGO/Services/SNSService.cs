using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class SNSService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "Simple Notification Service (Amazon SNS)";
        public string ServiceIdentifier => nameof(SNSService);

        public bool IsEmail => false;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("SNS_AccessKey", "Access key ID", MonitorSettingType.Text, ""),
            new("SNS_SecretKey", "Secret access key", MonitorSettingType.Secret, ""),
            new("SNS_Region", "Region", MonitorSettingType.Text, "us-west-2", "AWS region code, e.g. us-east-1. Must match the topic's region."),
            new("SNS_TopicArn", "Topic ARN", MonitorSettingType.Text, "", "Messages are published to this topic and delivered to its subscribers.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            using var client = new AmazonSimpleNotificationServiceClient(
                new BasicAWSCredentials(GetRequired("SNS_AccessKey"), GetRequired("SNS_SecretKey", secret: true)),
                RegionEndpoint.GetBySystemName(GetRequired("SNS_Region")));
            var request = new PublishRequest
            {
                TopicArn = GetRequired("SNS_TopicArn"),
                // SNS subjects are limited to 100 characters, and only some subscribers show them
                Subject = message.Subject.Length > 100 ? message.Subject[..100] : message.Subject,
                Message = message.Subject + "\n\n" + message.Body
            };

            await client.PublishAsync(request, ct);
            return true;
        }
    }
}
