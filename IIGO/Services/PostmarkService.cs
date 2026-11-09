using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using PostmarkDotNet;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class PostmarkService(ApplicationDbContext context, SecretProtector secrets) : ServiceBase(context, secrets), IMessengerService
    {
        public string ServiceName => "Postmark Email Relay";
        public string ServiceIdentifier => nameof(PostmarkService);

        public bool IsEmail => true;

        public override IReadOnlyList<MonitorSetting> Settings { get; } =
        [
            new("PM_ServerToken", "Server token", MonitorSettingType.Secret, ""),
            new("PM_FromAddress", "From address", MonitorSettingType.Email, "admin@example.com", "Must be a confirmed sender signature or on a verified domain."),
            new("PM_MessageStream", "Message stream", MonitorSettingType.Text, "outbound")
        ];

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            RequireRecipients(message);

            var msg = new PostmarkMessage
            {
                From = GetRequired("PM_FromAddress"),
                To = string.Join(",", message.Recipients),
                Subject = message.Subject,
                TextBody = message.Body,
                HtmlBody = message.HtmlBody,
                MessageStream = GetRequired("PM_MessageStream")
            };
            var client = new PostmarkClient(GetRequired("PM_ServerToken", secret: true));
            var resp = await client.SendMessageAsync(msg);
            if (resp.Status != PostmarkStatus.Success)
                throw new InvalidOperationException($"Postmark returned error {resp.ErrorCode}: {resp.Message}");

            return true;
        }
    }
}
