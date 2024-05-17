namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.Transport;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Infrastructure;

    class AuditIngestionFaultPolicy
    {
        readonly IFailedAuditStorage failedAuditStorage;
        readonly string logPath;
        readonly ImportFailureCircuitBreaker failureCircuitBreaker;

        public AuditIngestionFaultPolicy(IFailedAuditStorage failedAuditStorage, LoggingSettings settings, Func<string, Exception, Task> onCriticalError)
        {
            logPath = Path.Combine(settings.LogPath, @"FailedImports\Audit");
            this.failedAuditStorage = failedAuditStorage;

            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);

            Directory.CreateDirectory(logPath);
        }

        public async Task<ErrorHandleResult> OnError(ErrorContext errorContext, CancellationToken cancellationToken = default)
        {
            //Same as recoverability policy in NServiceBusFactory
            if (errorContext.ImmediateProcessingFailures < 3)
            {
                return ErrorHandleResult.RetryRequired;
            }

            await StoreFailedMessageDocument(errorContext, cancellationToken);
            return ErrorHandleResult.Handled;
        }

        Task StoreFailedMessageDocument(ErrorContext errorContext, CancellationToken cancellationToken)
        {
            var failure = new FailedAuditImport
            {
                Id = Guid.NewGuid().ToString(),
                Message = new FailedTransportMessage
                {
                    Id = errorContext.Message.MessageId,
                    Headers = errorContext.Message.Headers,
                    // At the moment we are taking a defensive copy of the body to avoid issues with the message body
                    // buffers being returned to the pool and potentially being overwritten. Once we know how RavenDB
                    // handles byte[] to ReadOnlyMemory<byte> conversion we might be able to remove this.
                    Body = errorContext.Message.Body.ToArray()
                }
            };

            return Handle(errorContext.Exception, failure, cancellationToken);
        }

        async Task Handle(Exception exception, FailedAuditImport failure, CancellationToken cancellationToken)
        {
            try
            {
                await DoLogging(exception, failure, cancellationToken);
            }
            finally
            {
                failureCircuitBreaker.Increment(exception);
            }
        }

        async Task DoLogging(Exception exception, FailedAuditImport failure, CancellationToken cancellationToken)
        {
            // Write to storage
            await failedAuditStorage.SaveFailedAuditImport(failure);

            // Write to Log Path
            var filePath = Path.Combine(logPath, failure.Id + ".txt");
            await File.WriteAllTextAsync(filePath, exception.ToFriendlyString(), cancellationToken);

            // Write to Event Log
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

#if DEBUG
        void WriteEvent(string message)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            EventSourceCreator.Create();

            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }
#else
        void WriteEvent(string message)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }
#endif
    }
}