using IIGO.Models;
using IIGO.Services.Interfaces;
using SendGrid.Helpers.Mail;
using SendGrid;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace IIGO.Services
{
    public class SendGridService : IMessengerService
    {
        public string ServiceName => nameof(SendGridService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var client = new SendGridClient("");
                var from = new EmailAddress("", "");
                var to = new EmailAddress("", "");
                var msg = MailHelper.CreateSingleEmail(from, to, "", "", "");
                var response = await client.SendEmailAsync(msg, ct);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }
    }
}
