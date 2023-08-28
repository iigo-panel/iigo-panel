using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class SESService : ServiceBase, IMessengerService
    {
        private readonly ApplicationDbContext _context;

        public SESService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => "Simple Email Service (Amazon SES)";
        public string ServiceIdentifier => nameof(SESService);

        public bool IsEmail => true;

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            Message msg = new Message(new Content(""), new Body() { Html = new Content("") });
            SendEmailRequest req = new SendEmailRequest("", new Destination(new List<string> { "" }), msg) { ReturnPath = "" };
            //if (!String.IsNullOrWhiteSpace(replyTo))
            //    req.ReplyToAddresses.Add(replyTo);
            using (AmazonSimpleEmailServiceClient client = new AmazonSimpleEmailServiceClient(new BasicAWSCredentials(GetSetting("SES_AccessKey", "AccessKey"), GetSetting("SES_SecretKey", "SecretKey")), RegionEndpoint.USWest2))
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

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SES_AccessKey") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SES_AccessKey", SettingValue = "AccessKey" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SES_SecretKey") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SES_SecretKey", SettingValue = "SecretKey" });

            _context.SaveChanges();
        }
    }
}
