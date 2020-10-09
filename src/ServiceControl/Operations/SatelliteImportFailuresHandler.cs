﻿namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class SatelliteImportFailuresHandler
    {
        IDocumentStore store;
        string logPath;

        ImportFailureCircuitBreaker failureCircuitBreaker;

        public SatelliteImportFailuresHandler(IDocumentStore store, LoggingSettings loggingSettings, Func<string, Exception, Task> onCriticalError)
        {
            this.store = store;
            logPath = Path.Combine(loggingSettings.LogPath, @"FailedImports\Error");

            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);

            Directory.CreateDirectory(logPath);
        }

        public Task Handle(ErrorContext errorContext)
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
            var documentId = $"FailedErrorImports/{id}";

            // Write to Raven
            using (var session = store.OpenAsyncSession())
            {
                failure.Id = documentId;

                await session.StoreAsync(failure)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            // Write to Log Path
            var filePath = Path.Combine(logPath, $"{id}.txt");
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