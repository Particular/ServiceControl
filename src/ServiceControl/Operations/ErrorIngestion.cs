﻿namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Metrics;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ErrorIngestion : IHostedService
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public ErrorIngestion(
            Settings settings,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            Metrics metrics,
            IErrorMessageDataStore dataStore,
            ErrorIngestionCustomCheck.State ingestionState,
            ErrorIngestor ingestor,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime)
        {
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            errorQueue = settings.ErrorQueue;
            this.ingestor = ingestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.applicationLifetime = applicationLifetime;

            receivedMeter = metrics.GetCounter("Error ingestion - received");
            batchSizeMeter = metrics.GetMeter("Error ingestion - batch size");
            batchDurationMeter = metrics.GetMeter("Error ingestion - batch processing duration", FrequencyInMilliseconds);

            if (!transportSettings.MaxConcurrency.HasValue)
            {
                throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
            }

            channel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(transportSettings.MaxConcurrency.Value)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            errorHandlingPolicy = new ErrorIngestionFaultPolicy(dataStore, settings.LoggingSettings, OnCriticalError);

            watchdog = new Watchdog("failed message ingestion", EnsureStarted, EnsureStopped, ingestionState.ReportError, ingestionState.Clear, settings.TimeToRestartErrorIngestionAfterFailure, Logger);

            ingestionWorker = Task.Run(() => Loop(), CancellationToken.None);
        }

        public Task StartAsync(CancellationToken _) => watchdog.Start(() => applicationLifetime.StopApplication());

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await watchdog.Stop();
            channel.Writer.Complete();
            await ingestionWorker;
            if (transportInfrastructure != null)
            {
                await transportInfrastructure.Shutdown(cancellationToken);
            }
        }

        async Task Loop()
        {
            var contexts = new List<MessageContext>(transportSettings.MaxConcurrency.Value);

            while (await channel.Reader.WaitToReadAsync())
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
                        await ingestor.Ingest(contexts);
                    }
                }
                catch (OperationCanceledException)
                {
                    //Do nothing as we are shutting down
                    continue;
                }
                catch (Exception e) // show must go on
                {
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info("Ingesting messages failed", e);
                    }

                    // signal all message handling tasks to terminate
                    foreach (var context in contexts)
                    {
                        context.GetTaskCompletionSource().TrySetException(e);
                    }
                }
                finally
                {
                    contexts.Clear();
                }
            }
            // will fall out here when writer is completed
        }

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);

                if (!unitOfWorkFactory.CanIngestMore())
                {
                    if (messageReceiver != null)
                    {
                        var stoppable = messageReceiver;
                        messageReceiver = null;
                        await stoppable.StopReceive(cancellationToken);
                        Logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down completed");
                    }
                    return;
                }

                if (messageReceiver != null)
                {
                    return; //Already started
                }

                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    errorQueue,
                    transportSettings,
                    EndpointType.Primary,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly);

                messageReceiver = transportInfrastructure.Receivers[errorQueue];

                if (settings.ForwardErrorMessages)
                {
                    await ingestor.VerifyCanReachForwardingAddress();
                }

                await messageReceiver.StartReceive(cancellationToken);

                Logger.Info("Ensure started. Infrastructure started");
            }
            catch
            {
                if (messageReceiver != null)
                {
                    await messageReceiver.StopReceive(cancellationToken);
                }

                messageReceiver = null; // Setting to null so that it doesn't exit when it retries in line 134

                throw;
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        async Task OnMessage(MessageContext messageContext, CancellationToken cancellationToken)
        {
            if (settings.MessageFilter != null && settings.MessageFilter(messageContext))
            {
                return;
            }

            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageContext.SetTaskCompletionSource(taskCompletionSource);

            receivedMeter.Mark();

            await channel.Writer.WriteAsync(messageContext, cancellationToken);
            await taskCompletionSource.Task;
        }

        Task OnCriticalError(string failure, Exception exception)
        {
            Logger.Fatal($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken);

                if (messageReceiver == null)
                {
                    return; //Already stopped
                }
                var stoppable = messageReceiver;
                messageReceiver = null;
                await stoppable.StopReceive(cancellationToken);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        string errorQueue;
        ErrorIngestionFaultPolicy errorHandlingPolicy;
        TransportInfrastructure transportInfrastructure;
        IMessageReceiver messageReceiver;

        readonly Settings settings;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly Watchdog watchdog;
        readonly Channel<MessageContext> channel;
        readonly Task ingestionWorker;
        readonly Meter batchDurationMeter;
        readonly Meter batchSizeMeter;
        readonly Counter receivedMeter;
        readonly ErrorIngestor ingestor;
        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly IHostApplicationLifetime applicationLifetime;
        static readonly ILog Logger = LogManager.GetLogger<ErrorIngestion>();
    }
}