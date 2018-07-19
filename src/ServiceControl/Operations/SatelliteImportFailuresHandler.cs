namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Installers;

    class SatelliteImportFailuresHandler
    {
        private IDocumentStore store;
        private string logPath;

        private Func<FailedTransportMessage, object> messageBuilder;
        private ImportFailureCircuitBreaker failureCircuitBreaker;

        public SatelliteImportFailuresHandler(IDocumentStore store, string logPath, Func<FailedTransportMessage, object> messageBuilder, CriticalError criticalError)
        {
            this.store = store;
            this.logPath = logPath;
            this.messageBuilder = messageBuilder;

            failureCircuitBreaker = new ImportFailureCircuitBreaker(criticalError);

            Directory.CreateDirectory(logPath);
        }

        public Task Handle(ErrorContext errorContext)
        {
            var failure = (dynamic) messageBuilder(new FailedTransportMessage
            {
                Id = errorContext.Message.MessageId,
                Headers = errorContext.Message.Headers,
                Body = errorContext.Message.Body
            });

            return Handle(errorContext.Exception, failure);
        }

        private async Task Handle(Exception exception, dynamic failure)
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

        private async Task DoLogging(Exception exception, dynamic failure)
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
        private async Task WriteEvent(string message)
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