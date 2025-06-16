namespace ServiceControl.Notifications.Email
{
    using System;
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class EmailSender(ILogger<EmailSender> logger)
    {
        public async Task Send(EmailNotifications settings, string subject, string body, string emailDropFolder = null)
        {
            try
            {
                using (var client = CreateSmtpClient(settings, emailDropFolder))
                using (var mailMessage = new MailMessage(settings.From, settings.To, subject, body))
                {
                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failure sending email.");
                throw;
            }
        }

        static SmtpClient CreateSmtpClient(EmailNotifications settings, string emailDropFolder)
        {
            if (emailDropFolder != null)
            {
                return new SmtpClient
                {
                    PickupDirectoryLocation = emailDropFolder,
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory
                };
            }

            var smtpClient = new SmtpClient(settings.SmtpServer, settings.SmtpPort ?? 25)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = settings.EnableTLS,
                Timeout = defaultTimeout
            };
            if (string.IsNullOrWhiteSpace(settings.AuthenticationAccount) == false)
            {
                smtpClient.Credentials = new NetworkCredential(settings.AuthenticationAccount, settings.AuthenticationPassword);
            }

            return smtpClient;
        }

        static int defaultTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
    }
}