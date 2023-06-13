using IIGO.Models;
using IIGO.Services.Interfaces;
using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class SMTPService : IMessengerService
    {
        public string ServiceName => nameof(SMTPService);

        public async Task<bool> SendMessageAsync(MessageData message, CancellationToken ct)
        {
            // TODO: Get settings from DB
            try
            {
                var email = new MimeMessage();

                email.From.Add(new MailboxAddress("", ""));
                email.Sender = new MailboxAddress("", "");

                email.To.Add(new MailboxAddress("", ""));

                email.Subject = "";

                var body = new BodyBuilder()
                {
                    HtmlBody = "",
                    TextBody = ""
                };
                email.Body = body.ToMessageBody();

                using var smtp = new SmtpClient();

                await smtp.ConnectAsync("", 587, true, ct);
                await smtp.AuthenticateAsync("", "", ct);
                await smtp.SendAsync(email, ct);
                await smtp.DisconnectAsync(true, ct);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
