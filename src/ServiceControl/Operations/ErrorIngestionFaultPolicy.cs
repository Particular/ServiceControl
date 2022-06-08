namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class ErrorIngestionFaultPolicy : IErrorHandlingPolicy
    {
        IDocumentStore store;
        string logPath;

        ImportFailureCircuitBreaker failureCircuitBreaker;

        public ErrorIngestionFaultPolicy(IDocumentStore store, LoggingSettings loggingSettings, Func<string, Exception, Task> onCriticalError)
        {
            this.store = store;
            logPath = Path.Combine(loggingSettings.LogPath, @"FailedImports\Error");

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

            await Handle(handlingContext.Error)
                .ConfigureAwait(false);
            return ErrorHandleResult.Handled;
        }

        Task Handle(ErrorContext errorContext)
        {
            var failure = new FailedErrorImport
            {
                Message = new FailedTransportMessage
                {
                    Id = errorContext.Message.MessageId,
                    Headers = errorContext.Message.Headers,
                    Body = errorContext.Message.Body
                }
            };

            return Handle(errorContext.Exception, failure);
        }

        async Task Handle(Exception exception, FailedErrorImport failure)
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

        async Task DoLogging(Exception exception, FailedErrorImport failure)
        {
            var id = Guid.NewGuid();

            // Write to Raven
            using (var session = store.OpenAsyncSession())
            {
                failure.Id = id;

                await session.StoreAsync(failure)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

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