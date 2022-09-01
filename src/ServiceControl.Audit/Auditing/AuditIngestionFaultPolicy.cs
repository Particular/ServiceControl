namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using ServiceControl.Audit.Persistence;

    class AuditIngestionFaultPolicy : IErrorHandlingPolicy
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

        public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
        {
            //Same as recoverability policy in NServiceBusFactory
            if (handlingContext.Error.ImmediateProcessingFailures < 3)
            {
                return ErrorHandleResult.RetryRequired;
            }

            await StoreFailedMessageDocument(handlingContext.Error)
                .ConfigureAwait(false);
            return ErrorHandleResult.Handled;
        }

        Task StoreFailedMessageDocument(ErrorContext errorContext)
        {
            var failure = (dynamic)messageBuilder(new FailedTransportMessage
            {
                Id = errorContext.Message.MessageId,
                Headers = errorContext.Message.Headers,
                Body = errorContext.Message.Body
            });

            return Handle(errorContext.Exception, failure);
        }

        async Task Handle(Exception exception, dynamic failure)
        {
            try
            {
                await DoLogging(exception, failure)
                    .ConfigureAwait(false);
            }
            finally
            {
                failureCircuitBreaker.Increment(exception);
            }
        }

        async Task DoLogging(Exception exception, dynamic failure)
        {
            var id = Guid.NewGuid().ToString();
            failure.Id = id;

            // Write to storage
            await failedAuditStorage.Store(failure).ConfigureAwait(false);

            // Write to Log Path
            var filePath = Path.Combine(logPath, failure.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());

            // Write to Event Log
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

#if DEBUG
        void WriteEvent(string message)
        {
            EventSource.Create();

            EventLog.WriteEntry(EventSource.SourceName, message, EventLogEntryType.Error);
        }
#else
        void WriteEvent(string message)
        {
            EventLog.WriteEntry(EventSource.SourceName, message, EventLogEntryType.Error);
        }
#endif
    }
}