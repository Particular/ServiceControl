namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class ErrorIngestionFaultPolicy
    {
        IErrorMessageDataStore store;
        string logPath;

        ImportFailureCircuitBreaker failureCircuitBreaker;

        public ErrorIngestionFaultPolicy(IErrorMessageDataStore store, LoggingSettings loggingSettings, Func<string, Exception, Task> onCriticalError)
        {
            this.store = store;
            logPath = Path.Combine(loggingSettings.LogPath, "FailedImports", "Error");

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

            await Handle(errorContext, cancellationToken);
            return ErrorHandleResult.Handled;
        }

        Task Handle(ErrorContext errorContext, CancellationToken cancellationToken)
        {
            var failure = new FailedErrorImport
            {
                Message = new FailedTransportMessage
                {
                    Id = errorContext.Message.MessageId,
                    Headers = errorContext.Message.Headers,
                    Body = errorContext.Message.Body.ToArray() // TODO NSB8 Can this be adjusted?
                }
            };

            return Handle(errorContext.Exception, failure, cancellationToken);
        }

        async Task Handle(Exception exception, FailedErrorImport failure, CancellationToken cancellationToken)
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
        async Task DoLogging(Exception exception, FailedErrorImport failure, CancellationToken cancellationToken)
#pragma warning restore IDE0060
        {
            failure.Id = FailedErrorImport.MakeDocumentId(Guid.NewGuid());

            // Write to data store
            await store.StoreFailedErrorImport(failure);

            // Write to Log Path
            var filePath = Path.Combine(logPath, failure.Id + ".txt");
            File.WriteAllText(filePath, exception.ToFriendlyString());

            // Write to Event Log
            WriteEvent("A message import has failed. A log file has been written to " + filePath);
        }

#if DEBUG
        void WriteEvent(string message)
        {
            EventSourceCreator.Create();

            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }
#else
        void WriteEvent(string message)
        {
            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }
#endif
    }
}