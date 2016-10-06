namespace ServiceControl.Operations.Error
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EventLog;
    using ServiceControl.EventLog.Definitions;
    using ServiceControl.Infrastructure;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    class ProcessErrors
    {
        private const int BATCH_SIZE = 64;

        private ILog logger = LogManager.GetLogger<ProcessErrors>();

        private readonly IDocumentStore store;
        private readonly ErrorIngestionCache errorIngestionCache;
        private readonly IBus bus;
        private Task task;
        private PatchCommandDataFactory patchCommandDataFactory;
        private volatile bool stop;
        private readonly Meter meter = Metric.Meter("Error messages processed", Unit.Custom("Messages"));
        private readonly CriticalError criticalError;
        private RepeatedFailuresOverTimeCircuitBreaker breaker;

        public ProcessErrors(IDocumentStore store, ErrorIngestionCache errorIngestionCache, PatchCommandDataFactory patchCommandDataFactory, IBus bus, CriticalError criticalError)
        {
            this.store = store;
            this.errorIngestionCache = errorIngestionCache;
            this.patchCommandDataFactory = patchCommandDataFactory;
            this.bus = bus;
            this.criticalError = criticalError;
        }

        public void Start()
        {
            breaker = new RepeatedFailuresOverTimeCircuitBreaker("ProcessErrors", TimeSpan.FromMinutes(2), ex =>
                {
                    stop = true;
                    criticalError.Raise("Repeated failures when processing errors.", ex);
                },
                TimeSpan.FromSeconds(2));
            stop = false;
            task = ProcessWithRetries();
        }

        public void Stop()
        {
            stop = true;
            task.Wait();
            breaker.Dispose();
        }

        private async Task ProcessWithRetries()
        {
            do
            {
                try
                {
                    await Process().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.Warn("ProcessErrors failed, having a break for 2 seconds before trying again.", ex);
                    await breaker.Failure(ex).ConfigureAwait(false);
                    logger.Warn("Restarting ProcessErrors.");
                }
            } while (!stop);
        }

        private async Task Process()
        {
            var processedFiles = new List<string>(BATCH_SIZE);
            var patchOperations = new List<ICommandData>(BATCH_SIZE);
            var recoverabilityBatchCommandFactory = new RecoverabilityBatchCommandFactory(BATCH_SIZE);
            var eventLogBatchCommandFactory = new EventLogBatchCommandFactory();

            do
            {
                recoverabilityBatchCommandFactory.StartNewBatch();

                var lazyBulkInsert = new Lazy<BulkInsertOperation>(() => store.BulkInsert(options: new BulkInsertOptions
                {
                    OverwriteExisting = true,
                    BatchSize = 64
                }));

                try
                {
                    foreach (var entry in errorIngestionCache.GetBatch(BATCH_SIZE))
                    {
                        if (stop)
                        {
                            break;
                        }

                        Dictionary<string, string> headers;
                        ClaimsCheck bodyStorageClaimsCheck;
                        bool recoverable;

                        if (errorIngestionCache.TryGet(entry, out headers, out recoverable, out bodyStorageClaimsCheck))
                        {
                            var uniqueMessageId = headers.UniqueMessageId();
                            var failureDetails = ParseFailureDetails(headers);

                            //var recoverabilityCommand = recoverabilityBatchCommandFactory.Create(uniqueMessageId, headers);
                            //if (recoverabilityCommand != null) //Repeated failure
                            //{
                            //    patchOperations.Add(recoverabilityCommand);

                            //    patchOperations.Add(patchCommandDataFactory.Patch(uniqueMessageId, headers, recoverable, bodyStorageClaimsCheck, failureDetails));
                            //}
                            //else // New failure
                            //{
                                lazyBulkInsert.Value.Store(patchCommandDataFactory.New(uniqueMessageId, headers, recoverable, bodyStorageClaimsCheck, failureDetails));
                            //}

                            lazyBulkInsert.Value.Store(eventLogBatchCommandFactory.Create(uniqueMessageId, failureDetails));

                            processedFiles.Add(entry);

                            meter.Mark();
                        }
                    }

                    if (patchOperations.Count > 0)
                    {
                        await store.AsyncDatabaseCommands.BatchAsync(patchOperations).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (lazyBulkInsert.IsValueCreated)
                    {
                        await lazyBulkInsert.Value.DisposeAsync().ConfigureAwait(false);
                    }
                }

                var processedFilesCount = processedFiles.Count;
                processedFiles.Clear();
                patchOperations.Clear();

                if (processedFilesCount > 0)
                {
                    recoverabilityBatchCommandFactory.CompleteBatch(bus);

                    foreach (var file in processedFiles)
                    {
                        File.Delete(file);
                    }
                }

                breaker.Success();

                if (!stop && processedFilesCount == 0)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            } while (!stop);
        }

        FailureDetails ParseFailureDetails(Dictionary<string, string> headers)
        {
            var result = new FailureDetails();

            DictionaryExtensions.CheckIfKeyExists("NServiceBus.TimeOfFailure", headers, s => result.TimeOfFailure = DateTimeExtensions.ToUtcDateTime(s));

            result.Exception = GetException(headers);

            result.AddressOfFailingEndpoint = headers[FaultsHeaderKeys.FailedQ];

            return result;
        }

        ExceptionDetails GetException(IReadOnlyDictionary<string, string> headers)
        {
            var exceptionDetails = new ExceptionDetails();
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.ExceptionType", headers,
                s => exceptionDetails.ExceptionType = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Message", headers,
                s => exceptionDetails.Message = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.Source", headers,
                s => exceptionDetails.Source = s);
            DictionaryExtensions.CheckIfKeyExists("NServiceBus.ExceptionInfo.StackTrace", headers,
                s => exceptionDetails.StackTrace = s);
            return exceptionDetails;
        }

    }

    class RecoverabilityBatchCommandFactory
    {
        private List<string> firstTimeFailureIds;
        private List<string> repeatedFailureIds;

        public RecoverabilityBatchCommandFactory(int batchSize)
        {
            firstTimeFailureIds = new List<string>(batchSize);
            repeatedFailureIds = new List<string>(batchSize);
        }

        public void StartNewBatch()
        {
            firstTimeFailureIds.Clear();
            repeatedFailureIds.Clear();
        }

        public ICommandData Create(string uniqueMessageId, Dictionary<string, string> headers)
        {
            string failedMessageId;
            if (headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
            {
                repeatedFailureIds.Add(failedMessageId);

                return new DeleteCommandData
                {
                    Key = FailedMessageRetry.MakeDocumentId(failedMessageId)
                };
            }

            firstTimeFailureIds.Add(uniqueMessageId);
            return null;
        }

        public void CompleteBatch(IBus bus)
        {
            bus.Publish(new FailedMessagesImported
            {
                NewFailureIds = firstTimeFailureIds.ToArray(),
                RepeatedFailureIds = repeatedFailureIds.ToArray()
            });
        }
    }

    class EventLogBatchCommandFactory
    {
        public EventLogItem Create(string failedMessageId, FailureDetails failureDetails)
        {
            var messageFailed = new MessageFailed
            {
                EndpointId = Address.Parse(failureDetails.AddressOfFailingEndpoint).Queue,
                FailedMessageId = failedMessageId,
                FailureDetails = failureDetails
            };

            return new MessageFailedDefinition().Apply(Guid.NewGuid().ToString(), messageFailed);
        }
    }
}