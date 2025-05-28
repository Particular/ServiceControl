using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NServiceBus.Logging;
using NServiceBus.Transport;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.Metrics;
using ServiceControl.Persistence;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.Transports;

namespace ServiceControl.Operations
{
    class ExtraExceptionDataIngestion : ErrorIngestion
    {
        public ExtraExceptionDataIngestion(Settings settings, ITransportCustomization transportCustomization,
            TransportSettings transportSettings, Metrics metrics, IErrorMessageDataStore dataStore,
            ErrorIngestionCustomCheck.State ingestionState,
            IIngestionUnitOfWorkFactory unitOfWorkFactory, IHostApplicationLifetime applicationLifetime,
            ErrorQueueDiscoveryExecutor errorQueueDiscoveryExecutor) : base(settings, transportCustomization,
            transportSettings, metrics, dataStore, ingestionState, null, unitOfWorkFactory, applicationLifetime,
            errorQueueDiscoveryExecutor)
        {
            Console.WriteLine("We did it?");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var contexts = new List<MessageContext>(transportSettings.MaxConcurrency.Value);

                while (await channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // will only enter here if there is something to read.
                    try
                    {
                        // as long as there is something to read this will fetch up to MaximumConcurrency items
                        while (channel.Reader.TryRead(out var context))
                        {
                            contexts.Add(context);
                        }

                        batchSizeMeter.Mark(contexts.Count);
                        using (batchDurationMeter.Measure())
                        {
                            // await ingestor.Ingest(contexts, stoppingToken);

                            
                            /* Normal Execution path:
                             * ErrorIngestor.Ingest |>
                             *  PersistFailedMessages |>
                             *      ErrorProcessor.Process |>
                             *          ErrorProcessor.ProcessMessage |>
                             *              FailedMessageFactory.ParseFailureDetails |>
                             *                  .GetException <- Here is where the exception data gets parsed
                             *              RavenRecoverabilityIngestionUnitOfWork.RecordFailedProcessingAttempt <- Fully formed failure gets stored here.
                             * 
                             */
                            Ingest(contexts, stoppingToken);
                        }
                    }
                    catch (Exception e)
                    {
                        // signal all message handling tasks to terminate
                        foreach (var context in contexts)
                        {
                            _ = context.GetTaskCompletionSource().TrySetException(e);
                        }

                        if (e is OperationCanceledException && stoppingToken.IsCancellationRequested)
                        {
                            Logger.Info("Batch cancelled", e);
                            break;
                        }

                        Logger.Info("Ingesting messages failed", e);
                    }
                    finally
                    {
                        contexts.Clear();
                    }
                }
                // will fall out here when writer is completed
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // ExecuteAsync cancelled
            } 
        }

        async Task Ingest(List<MessageContext> contexts, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>(contexts.Count);
            foreach (var context in contexts)
            {
                var existingMessageContext = await TryGetMessageStub(context);
                if (existingMessageContext != null)
                {
                    tasks.Add(StoreCombinedContext(existingMessageContext, context));
                }
                else
                {
                    tasks.Add(StoreExceptionStub(context));
                }
            }
            await Task.WhenAll(tasks);
            
        }

        async Task<MessageContext> TryGetMessageStub(MessageContext context) => null;

        async Task StoreCombinedContext(MessageContext messageStub, MessageContext exceptionStub)
        {
            // Mush them together make NServiceBusCompliant message
            // call ingestor.Ingest()
            // Do logic to store this.
        }

        async Task StoreExceptionStub(MessageContext context)
        {
            // Store this in RavenDB
        }

        // static readonly ILog Logger = LogManager.GetLogger<ErrorIngestion>();

        // public override async Task StartAsync(CancellationToken cancellationToken)
        // {
        //     await watchdog.Start(() => applicationLifetime.StopApplication(), cancellationToken);
        //     await base.StartAsync(cancellationToken);
        // }
        // public override async Task StopAsync(CancellationToken cancellationToken)
        // {
        //     try
        //     {
        //         await watchdog.Stop(cancellationToken);
        //         channel.Writer.Complete();
        //         await base.StopAsync(cancellationToken);
        //     }
        //     finally
        //     {
        //         if (transportInfrastructure != null)
        //         {
        //             try
        //             {
        //                 await transportInfrastructure.Shutdown(cancellationToken);
        //             }
        //             catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        //             {
        //                 Logger.Info("Shutdown cancelled", e);
        //             }
        //         }
        //     }
        // }

    }
}