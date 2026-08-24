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
    using Metrics;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Installers;

    class ErrorIngestionFaultPolicy
    {
        IFailedErrorImportDataStore store;
        string logPath;

        ImportFailureCircuitBreaker failureCircuitBreaker;

        public ErrorIngestionFaultPolicy(IFailedErrorImportDataStore store, LoggingSettings loggingSettings, Func<string, Exception, CancellationToken, Task> onCriticalError, IngestionMetrics metrics, ILogger logger)
        {
            this.store = store;
            this.metrics = metrics;
            this.logger = logger;
            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);

            if (!AppEnvironment.RunningInContainer)
            {
                logPath = Path.Combine(loggingSettings.LogPath, "FailedImports", "Error");
                Directory.CreateDirectory(logPath);
            }
        }

        public async Task<ErrorHandleResult> OnError(ErrorContext errorContext, CancellationToken cancellationToken = default)
        {
            using var failureMetrics = metrics.BeginErrorHandling(errorContext);

            //Same as recoverability policy in NServiceBusFactory
            if (errorContext.ImmediateProcessingFailures < 3)
            {
                failureMetrics.Retry();
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
                    Id = errorContext.MessageId,
                    Headers = errorContext.Headers,
                    Body = errorContext.Body.ToArray()
                },
                ExceptionInfo = errorContext.Exception.ToFriendlyString(),
                Id = FailedErrorImport.DeriveKey(errorContext.Headers, errorContext.MessageId).ToString()
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
            logger.LogError(exception, "Failed importing error message");

            // Write to data store
            await store.StoreFailedErrorImport(failure, cancellationToken);

            if (!AppEnvironment.RunningInContainer)
            {
                // Write to Log Path
                string filePath = Path.Combine(logPath, $"FailedErrorImports_{failure.Id.Replace("/", "_")}.txt");
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

        readonly IngestionMetrics metrics;
        readonly ILogger logger;
    }
}