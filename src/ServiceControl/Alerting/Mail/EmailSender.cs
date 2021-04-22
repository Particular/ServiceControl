namespace ServiceControl.Alerting.Mail
{
    using System;
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class EmailSender
    {
        public static Task Send(EmailNotifications settings, string subject, string body, string emailDropFolder = null)
        {
            try
            {
                using (var client = CreateSmtpClient(settings, emailDropFolder))
                {
                    using (var mailMessage = new MailMessage(settings.From, settings.To, subject, body))
                    {
                        return client.SendMailAsync(mailMessage);
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("Failure sending email.", e);
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
                EnableSsl = settings.EnableSSL,
            };
            if (string.IsNullOrWhiteSpace(settings.AuthenticationAccount) == false)
            {
                smtpClient.Credentials = new NetworkCredential(settings.AuthenticationAccount, settings.AuthenticationPassword);
            }

            smtpClient.Timeout = defaultTimeout;

            return smtpClient;
        }

        static ILog log = LogManager.GetLogger<EmailSender>();
        static int defaultTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
    }
}