using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using MimeKit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class SESService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "Simple Email Service (Amazon SES)";
        public string ServiceIdentifier => nameof(SESService);

        public bool IsEmail => true;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("SES_AccessKey", "Access key ID", MonitorSettingType.Text, ""),
            new("SES_SecretKey", "Secret access key", MonitorSettingType.Secret, ""),
            new("SES_Region", "Region", MonitorSettingType.Text, "us-west-2", "AWS region code, e.g. us-east-1."),
            new("SES_FromName", "From name", MonitorSettingType.Text, "IIGO Panel"),
            new("SES_FromAddress", "From address", MonitorSettingType.Email, "admin@example.com", "Must be a verified identity in SES.")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            RequireRecipients(message);

            var source = new MailboxAddress(GetSetting("SES_FromName"), GetRequired("SES_FromAddress")).ToString();
            var msg = new Message(new Content(message.Subject), new Body { Text = new Content(message.Body), Html = new Content(message.HtmlBody) });
            var req = new SendEmailRequest(source, new Destination(message.Recipients), msg);

            using var client = new AmazonSimpleEmailServiceClient(
                new BasicAWSCredentials(GetRequired("SES_AccessKey"), GetRequired("SES_SecretKey", secret: true)),
                RegionEndpoint.GetBySystemName(GetRequired("SES_Region")));
            await client.SendEmailAsync(req, ct);
            return true;
        }
    }
}
