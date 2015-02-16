namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Transports;
    using ServiceBus.Management.Infrastructure.Installers;

    public class SatelliteImportFailuresHandler : IManageMessageFailures, IDisposable
    {
        public SatelliteImportFailuresHandler( ISendMessages forwarder, Address failedImportQueue, string logPath)
        {
            this.forwarder = forwarder;
            this.failedImportQueue = failedImportQueue;
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

        public void FailedToReceive(Exception exception)
        {
            try
            {
                var id = Guid.NewGuid();

                var filePath = Path.Combine(logPath, id + ".txt");
                File.WriteAllText(filePath, exception.ToFriendlyString());
                WriteEvent("A message import has failed. A log file has been written to " + filePath);
            }
            finally
            {
                failureCircuitBreaker.Increment(exception);
            }
        }

        public void Init(Address address)
        {
        }

        public void Log(TransportMessage message, Exception e)
        {
            DoLogging(e, message, logPath);
        }

        void Handle(Exception exception, TransportMessage message, string logDirectory)
        {
            try
            {
                DoLogging(exception, message, logDirectory);
            }
            finally
            {
                failureCircuitBreaker.Increment(exception);
            }
        }

        void DoLogging(Exception exception, TransportMessage message, string logDirectory)
        {
            var id = Guid.NewGuid();

            forwarder.Send(message, failedImportQueue);
            var filePath = Path.Combine(logDirectory, id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

        static void WriteEvent(string message)
        {
#if DEBUG
            new CreateEventSource().Install(null);
#endif
            EventLog.WriteEntry(CreateEventSource.SourceName, message, EventLogEntryType.Error);
        }

        readonly ImportFailureCircuitBreaker failureCircuitBreaker = new ImportFailureCircuitBreaker();
        readonly ISendMessages forwarder;
        readonly Address failedImportQueue;
        readonly string logPath;
    }
}