namespace ServiceControl.Alerting.Mail
{
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;

    static class EmailSender
    {
        public static Task Send(AlertingSettings settings, string subject, string body, string emailDropFolder = null)
        {
            using (var client = CreateSmtpClient(settings, emailDropFolder))
            {
                using (var mailMessage = new MailMessage(settings.From, settings.To, subject, body))
                {
                    return client.SendMailAsync(mailMessage);
                }
            }
        }

        static SmtpClient CreateSmtpClient(AlertingSettings settings, string emailDropFolder)
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
            if (settings.AuthenticationEnabled)
            {
                smtpClient.Credentials = new NetworkCredential(settings.AuthenticationAccount, settings.AuthenticationPassword);
            }

            return smtpClient;
        }
    }
}