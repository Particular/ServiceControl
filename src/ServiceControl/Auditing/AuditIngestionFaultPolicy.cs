namespace ServiceControl.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceControl.Auditing.Metrics;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class AuditIngestionFaultPolicy
    {
        public AuditIngestionFaultPolicy(
            IFailedAuditImportDataStore store,
            LoggingSettings loggingSettings,
            Func<string, Exception, CancellationToken, Task> onCriticalError,
            AuditIngestionMetrics metrics,
            ILogger logger)
        {
            this.store = store;
            this.metrics = metrics;
            this.logger = logger;
            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);

            if (!AppEnvironment.RunningInContainer)
            {
                logPath = Path.Combine(loggingSettings.LogPath, "FailedImports", "Audit");
                Directory.CreateDirectory(logPath);
            }
        }

        public async Task<ErrorHandleResult> OnError(ErrorContext errorContext, CancellationToken cancellationToken = default)
        {
            using var errorMetrics = metrics.BeginErrorHandling(errorContext);

            //Same as recoverability policy in NServiceBusFactory
            if (errorContext.ImmediateProcessingFailures < 3)
            {
                errorMetrics.Retry();
                return ErrorHandleResult.RetryRequired;
            }

            await Handle(errorContext, cancellationToken);
            return ErrorHandleResult.Handled;
        }

        async Task Handle(ErrorContext errorContext, CancellationToken cancellationToken)
        {
            var failure = new FailedAuditImport
            {
                Message = new FailedTransportMessage
                {
                    Id = errorContext.MessageId,
                    Headers = errorContext.Headers,
                    Body = errorContext.Body.ToArray()
                },
                ExceptionInfo = errorContext.Exception.ToFriendlyString(),
                Id = FailedAuditImport.DeriveKey(errorContext.Headers, errorContext.MessageId).ToString()
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

        async Task DoLogging(Exception exception, FailedAuditImport failure, CancellationToken cancellationToken)
        {
            logger.LogError(exception, "Failed importing audit message");

            await store.StoreFailedAuditImport(failure, cancellationToken);

            if (!AppEnvironment.RunningInContainer)
            {
                var filePath = Path.Combine(logPath, $"FailedAuditImports_{failure.Id.Replace("/", "_")}.txt");
                await File.WriteAllTextAsync(filePath, failure.ExceptionInfo, cancellationToken);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WriteToEventLog($"An audit message import has failed. A log file has been written to {filePath}");
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

        readonly IFailedAuditImportDataStore store;
        readonly AuditIngestionMetrics metrics;
        readonly ImportFailureCircuitBreaker failureCircuitBreaker;
        readonly string logPath;
        readonly ILogger logger;
    }
}
