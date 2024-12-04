namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Configuration;
    using Infrastructure;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Installers;

    class ErrorIngestionFaultPolicy
    {
        IErrorMessageDataStore store;
        string logPath;

        ImportFailureCircuitBreaker failureCircuitBreaker;

        public ErrorIngestionFaultPolicy(IErrorMessageDataStore store, LoggingSettings loggingSettings, Func<string, Exception, Task> onCriticalError)
        {
            this.store = store;
            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);

            if (!AppEnvironment.RunningInContainer)
            {
                logPath = Path.Combine(loggingSettings.LogPath, "FailedImports", "Error");
                Directory.CreateDirectory(logPath);
            }
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

        async Task Handle(ErrorContext errorContext, CancellationToken cancellationToken)
        {
            var failure = new FailedErrorImport
            {
                Message = new FailedTransportMessage
                {
                    Id = errorContext.Message.MessageId,
                    Headers = errorContext.Message.Headers,
                    Body = errorContext.Message.Body.ToArray()
                },
                ExceptionInfo = errorContext.Exception.ToFriendlyString(),
                Id = FailedErrorImport.MakeDocumentId(Guid.NewGuid())
            };

            try
            {
                await DoLogging(errorContext.Exception, failure, cancellationToken);
            }
            finally
            {
                failureCircuitBreaker.Increment(errorContext.Exception);
            }
        }

        async Task DoLogging(Exception exception, FailedErrorImport failure, CancellationToken cancellationToken)
        {
            log.Error("Failed importing error message", exception);

            // Write to data store
            await store.StoreFailedErrorImport(failure);

            if (!AppEnvironment.RunningInContainer)
            {
                // Write to Log Path
                string filePath = Path.Combine(logPath, $"{failure.Id.Replace("/", "_")}.txt");
                await File.WriteAllTextAsync(filePath, failure.ExceptionInfo, cancellationToken);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WriteToEventLog($"A message import has failed. A log file has been written to {filePath}");
                }
            }
        }

        [SupportedOSPlatform("windows")]
        static void WriteToEventLog(string message)
        {
#if DEBUG
            EventSourceCreator.Create();
#endif
            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }

        static readonly ILog log = LogManager.GetLogger<ErrorIngestionFaultPolicy>();
    }
}