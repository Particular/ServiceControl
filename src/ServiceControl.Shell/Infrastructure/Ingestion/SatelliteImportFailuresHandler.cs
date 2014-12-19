namespace ServiceControl.Shell.Infrastructure.Ingestion
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Faults;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Installers;

    class SatelliteImportFailuresHandler : IManageMessageFailures, IDisposable
    {
        public SatelliteImportFailuresHandler(IDocumentStore store, string queue,  string logPath)
        {
            this.store = store;
            this.queue = queue;
            this.logPath = logPath;
            
            Directory.CreateDirectory(logPath);
        }

        public void Dispose()
        {
            failureCircuitBreaker.Dispose();
        }

        public void SerializationFailedForMessage(TransportMessage message, Exception e)
        {
            Handle(e, message, logPath);
        }

        public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
        {
            Handle(e, message, logPath);
        }

        public void Init(Address address)
        {
        }


        void Handle(Exception exception, TransportMessage message, string logDirectory)
        {
            try
            {
                Store(exception, new FailedIngestion()
                {
                    Id = Guid.NewGuid(),
                    Message = message,
                    SourceQueue = queue
                }, logDirectory);
            }
            finally
            {
                failureCircuitBreaker.Increment(exception);
            }
        }

        void Store(Exception exception, FailedIngestion failure, string logDirectory)
        {
            using (var session = store.OpenSession())
            {
                session.Store(failure);
                session.SaveChanges();
            }

            var filePath = Path.Combine(logDirectory, failure.Id + ".txt");
            File.WriteAllText(filePath, FormatException(exception));
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

        static void WriteEvent(string message)
        {
#if DEBUG
            new CreateEventSource().Install(null);
#endif
            EventLog.WriteEntry(CreateEventSource.SourceName, message, EventLogEntryType.Error);
        }

        public static string FormatException(Exception exception)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Exception:");
            stringBuilder.Append(Environment.NewLine);
            while (exception != null)
            {
                stringBuilder.AppendLine(exception.Message);

                foreach (var data in exception.Data)
                {
                    stringBuilder.Append("Data :");
                    stringBuilder.AppendLine(data.ToString());
                }

                if (exception.StackTrace != null)
                {
                    stringBuilder.AppendLine("StackTrace:");
                    stringBuilder.AppendLine(exception.StackTrace);
                }

                if (exception.Source != null)
                {
                    stringBuilder.AppendLine("Source:");
                    stringBuilder.AppendLine(exception.Source);
                }

                if (exception.TargetSite != null)
                {
                    stringBuilder.AppendLine("TargetSite:");
                    stringBuilder.AppendLine(exception.TargetSite.ToString());
                }

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        readonly ImportFailureCircuitBreaker failureCircuitBreaker = new ImportFailureCircuitBreaker();
        readonly string logPath;
        readonly IDocumentStore store;
        readonly string queue;
    }
}