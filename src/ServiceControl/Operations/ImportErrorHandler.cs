namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Net.Mail;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ImportErrorHandler
    {
        public string ErrorLogDirectory;
        public string AuditLogDirectory;
        public Func<SmtpClient> GetSmtpClient = () => new SmtpClient(); 
        public ImportErrorHandler()
        {
            AuditLogDirectory = Path.Combine(Settings.LogPath, @"FailedImports\Audit");
            ErrorLogDirectory = Path.Combine(Settings.LogPath, @"FailedImports\Error");
            Directory.CreateDirectory(AuditLogDirectory);
            Directory.CreateDirectory(ErrorLogDirectory);
        }

        public IDocumentSession Session { get; set; }

        public void HandleAudit(TransportMessage message, Exception exception)
        {
            var failedAuditImport = new FailedAuditImport
                {
                    Id = message.Id,
                    Message = message,
                };
            Handle(message, exception, failedAuditImport, AuditLogDirectory);
        }

        public void HandleError(TransportMessage message, Exception exception)
        {
            var failedErrorImport = new FailedErrorImport
                {
                    Id = message.Id,
                    Message = message,
                };
            Handle(message, exception, failedErrorImport, ErrorLogDirectory);
        }

        void Handle(TransportMessage message, Exception exception, object failure, string logDirectory)
        {
            Session.Store(failure);
            var filePath = Path.Combine(logDirectory, message.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());
            SendEmail("A message import has failed. A log file has been written to " + filePath);
        }

        void SendEmail(string message)
        {
            if (Settings.Email == null)
            {
                return;
            }
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(Settings.Email);
                mail.To.Add(new MailAddress(Settings.Email));
                mail.Subject = "An error occurred in ServiceControl";
                mail.Body = message;
                using (var client = GetSmtpClient())
                {
                    client.Send(mail);
                }
            }
        }
    }

    public class FailedErrorImport
    {
        public string Id { get; set; }
        public TransportMessage Message { get; set; }
    }

    public class FailedAuditImport
    {
        public string Id { get; set; }
        public TransportMessage Message { get; set; }
    }
}