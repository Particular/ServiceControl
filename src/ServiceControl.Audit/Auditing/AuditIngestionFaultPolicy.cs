namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Infrastructure.Installers;
    using Infrastructure.Settings;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using Raven.Client;

    class AuditIngestionFaultPolicy : IErrorHandlingPolicy
    {
        IDocumentStore store;
        string logPath;
        Func<FailedTransportMessage, object> messageBuilder;
        ImportFailureCircuitBreaker failureCircuitBreaker;

        public AuditIngestionFaultPolicy(IDocumentStore store, LoggingSettings settings, Func<FailedTransportMessage, object> messageBuilder, Func<string, Exception, Task> onCriticalError)
        {
            this.store = store;
            this.logPath = Path.Combine(settings.LogPath, @"FailedImports\Audit");
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
            await handlingContext.MoveToErrorQueue(handlingContext.FailedQueue, false)
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
                await failureCircuitBreaker.Increment(exception).ConfigureAwait(false);
            }
        }

        async Task DoLogging(Exception exception, dynamic failure)
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
            await WriteEvent("A message import has failed. A log file has been written to " + filePath)
                .ConfigureAwait(false);
        }

#if DEBUG
        async Task WriteEvent(string message)
        {
            await new CreateEventSource().Install(null)
                .ConfigureAwait(false);

            EventLog.WriteEntry(CreateEventSource.SourceName, message, EventLogEntryType.Error);
        }
#else
        private Task WriteEvent(string message)
        {
            EventLog.WriteEntry(CreateEventSource.SourceName, message, EventLogEntryType.Error);

            return Task.FromResult(0);
        }
#endif
    }
}