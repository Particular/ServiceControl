namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus.Transport;
    using ServiceControl.Audit.Persistence;

    class AuditIngestionFaultPolicy
    {
        IFailedAuditStorage failedAuditStorage;
        string logPath;
        Func<FailedTransportMessage, object> messageBuilder;
        ImportFailureCircuitBreaker failureCircuitBreaker;

        public AuditIngestionFaultPolicy(IFailedAuditStorage failedAuditStorage, LoggingSettings settings, Func<FailedTransportMessage, object> messageBuilder, Func<string, Exception, Task> onCriticalError)
        {
            logPath = Path.Combine(settings.LogPath, @"FailedImports\Audit");
            this.failedAuditStorage = failedAuditStorage;
            this.messageBuilder = messageBuilder;

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
            var failure = (dynamic)messageBuilder(new FailedTransportMessage
            {
                Id = errorContext.Message.MessageId,
                Headers = errorContext.Message.Headers,
                Body = errorContext.Message.Body.ToArray() //TODO Can this be adjusted?
            });

            return Handle(errorContext.Exception, failure, cancellationToken);
        }

        async Task Handle(Exception exception, dynamic failure, CancellationToken cancellationToken)
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

#pragma warning disable IDE0060
        async Task DoLogging(Exception exception, dynamic failure, CancellationToken cancellationToken)
#pragma warning restore IDE0060
        {
            var id = Guid.NewGuid().ToString();
            failure.Id = id;

            // Write to storage
            await failedAuditStorage.Store(failure);

            // Write to Log Path
            var filePath = Path.Combine(logPath, failure.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());

            // Write to Event Log
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

#if DEBUG
        void WriteEvent(string message)
        {
            // TODO: Figure a way to achieve something but in the linux way
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            EventSource.Create();

            EventLog.WriteEntry(EventSource.SourceName, message, EventLogEntryType.Error);
        }
#else
        void WriteEvent(string message)
        {
            // TODO: Figure a way to achieve something but in the linux way
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            EventLog.WriteEntry(EventSource.SourceName, message, EventLogEntryType.Error);
        }
#endif
    }
}