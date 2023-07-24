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
    internal class SNSService : ServiceBase, IMessengerService
    {
        private readonly ApplicationDbContext _context;
        public SNSService(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public string ServiceName => nameof(SNSService);

        public bool IsEmail => false;

        public void Initialize()
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SNS_AccessKey") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SNS_AccessKey", SettingValue = "AccessKey" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SNS_SecretKey") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SNS_SecretKey", SettingValue = "SecretKey" });
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == "SNS_TopicArn") == null)
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = "SNS_TopicArn", SettingValue = "TopicArn" });
            _context.SaveChanges();
        }

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                string messageText = "";

                IAmazonSimpleNotificationService client = new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials(GetSetting("SNS_AccessKey"), GetSetting("SNS_SecretKey")), RegionEndpoint.USWest2);
                var request = new PublishRequest
                {
                    TopicArn = GetSetting("SNS_TopicArn"),
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
