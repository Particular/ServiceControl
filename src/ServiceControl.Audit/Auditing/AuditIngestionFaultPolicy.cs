﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;

    class AuditIngestionFaultPolicy
    {
        readonly IFailedAuditStorage failedAuditStorage;
        readonly string logPath;
        readonly ImportFailureCircuitBreaker failureCircuitBreaker;

        public AuditIngestionFaultPolicy(IFailedAuditStorage failedAuditStorage, LoggingSettings settings, Func<string, Exception, Task> onCriticalError)
        {
            failureCircuitBreaker = new ImportFailureCircuitBreaker(onCriticalError);
            this.failedAuditStorage = failedAuditStorage;

            if (!AppEnvironment.RunningInContainer)
            {
                logPath = Path.Combine(settings.LogPath, @"FailedImports\Audit");
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

            await StoreFailedMessageDocument(errorContext, cancellationToken);
            return ErrorHandleResult.Handled;
        }

        async Task StoreFailedMessageDocument(ErrorContext errorContext, CancellationToken cancellationToken)
        {
            var failure = new FailedAuditImport
            {
                Id = Guid.NewGuid().ToString(),
                Message = new FailedTransportMessage
                {
                    Id = errorContext.Message.MessageId,
                    Headers = errorContext.Message.Headers,
                    // At the moment we are taking a defensive copy of the body to avoid issues with the message body
                    // buffers being returned to the pool and potentially being overwritten. Once we know how RavenDB
                    // handles byte[] to ReadOnlyMemory<byte> conversion we might be able to remove this.
                    Body = errorContext.Message.Body.ToArray()
                },
                ExceptionInfo = errorContext.Exception.ToFriendlyString()
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
            log.Error("Failed importing error message", exception);

            // Write to storage
            await failedAuditStorage.SaveFailedAuditImport(failure);

            if (!AppEnvironment.RunningInContainer)
            {
                // Write to Log Path
                var filePath = Path.Combine(logPath, failure.Id + ".txt");
                await File.WriteAllTextAsync(filePath, failure.ExceptionInfo, cancellationToken);

                // Write to Event Log
                WriteEvent("A message import has failed. A log file has been written to " + filePath);
            }
        }

        void WriteEvent(string message)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

#if DEBUG
            EventSourceCreator.Create();
#endif

            EventLog.WriteEntry(EventSourceCreator.SourceName, message, EventLogEntryType.Error);
        }

        static readonly ILog log = LogManager.GetLogger<AuditIngestionFaultPolicy>();
    }
}