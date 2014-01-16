namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ImportErrorHandler
    {
        public string ErrorLogDirectory;
        public string AuditLogDirectory;

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
            Session.Store(failedAuditImport);
            var filePath = Path.Combine(AuditLogDirectory, message.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());
        }

        public void HandleError(TransportMessage message, Exception exception)
        {
            var failedErrorImport = new FailedErrorImport
                {
                    Id = message.Id,
                    Message = message,
                };
            Session.Store(failedErrorImport);
            var filePath = Path.Combine(ErrorLogDirectory, message.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());
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