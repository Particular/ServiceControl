namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class Registrations : INeedInitialization
    {
        public void Init()
        {
            Configure.Instance.Configurer.ConfigureComponent<ImportErrorHandler>(DependencyLifecycle.SingleInstance);
            Configure.Instance.Configurer.ConfigureComponent<ImportFailureCircuitBreaker>(DependencyLifecycle.SingleInstance);
        }
    }

    public class ImportErrorHandler
    {
        public string ErrorLogDirectory;
        public string AuditLogDirectory;
        public ImportFailureCircuitBreaker ImportFailureCircuitBreaker { get; set; }

        public ImportErrorHandler()
        {
            AuditLogDirectory = Path.Combine(Settings.LogPath, @"FailedImports\Audit");
            ErrorLogDirectory = Path.Combine(Settings.LogPath, @"FailedImports\Error");
            Directory.CreateDirectory(AuditLogDirectory);
            Directory.CreateDirectory(ErrorLogDirectory);
        }

        public IDocumentStore Store { get; set; }

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
            try
            {
                using (var session = Store.OpenSession())
                {
                    session.Store(failure);
                    session.SaveChanges();
                }

                var filePath = Path.Combine(logDirectory, message.Id + ".txt");
                File.WriteAllText(filePath, exception.ToFriendlyString());
                WriteEvent("A message import has failed. A log file has been written to " + filePath);
            }
            finally
            {
                ImportFailureCircuitBreaker.Increment(exception);
            }
        }

        static void WriteEvent(string message)
        {
#if DEBUG
            new CreateEventSource().Install(null);
#endif
            EventLog.WriteEntry(CreateEventSource.SourceName, message, EventLogEntryType.Error);
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