﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Persistence;
    using Persistence.UnitOfWork;
    using ServiceControl.Infrastructure;
    using Transports;

    class AuditIngestion : IHostedService
    {
        public AuditIngestion(
            Settings settings,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            IFailedAuditStorage failedImportsStorage,
            AuditIngestionCustomCheck.State ingestionState,
            AuditIngestor auditIngestor,
            IAuditIngestionUnitOfWorkFactory unitOfWorkFactory,
            IHostApplicationLifetime applicationLifetime)
        {
            inputEndpoint = settings.AuditQueue;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.auditIngestor = auditIngestor;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.settings = settings;
            this.applicationLifetime = applicationLifetime;

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

            errorHandlingPolicy = new AuditIngestionFaultPolicy(failedImportsStorage, settings.LoggingSettings, OnCriticalError);

            watchdog = new Watchdog(
                "audit message ingestion",
                EnsureStarted,
                EnsureStopped,
                ingestionState.ReportError,
                ingestionState.Clear,
                settings.TimeToRestartAuditIngestionAfterFailure,
                logger
            );
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ingestionWorker = Loop(stopSource.Token);
            await watchdog.Start(() => applicationLifetime.StopApplication());
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await stopSource.CancelAsync();
            await watchdog.Stop();
            channel.Writer.Complete();
            await ingestionWorker;

            if (transportInfrastructure != null)
            {
                await transportInfrastructure.Shutdown(cancellationToken);
            }
        }

        Task OnCriticalError(string failure, Exception exception)
        {
            logger.Fatal($"OnCriticalError. '{failure}'", exception);
            return watchdog.OnFailure(failure);
        }

        async Task EnsureStarted(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Debug("Ensure started. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken);
                logger.Debug("Ensure started. Start/stop semaphore acquired");

                if (!unitOfWorkFactory.CanIngestMore())
                {
                    if (queueIngestor != null)
                    {
                        var stoppable = queueIngestor;
                        queueIngestor = null;
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down commencing");
                        await stoppable.StopReceive(cancellationToken);
                        logger.Info("Shutting down due to failed persistence health check. Infrastructure shut down completed");
                    }

                    return;
                }

                if (queueIngestor != null)
                {
                    logger.Debug("Ensure started. Already started, skipping start up");
                    return; //Already started
                }

                logger.Info("Ensure started. Infrastructure starting");

                transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(
                    inputEndpoint,
                    transportSettings,
                    OnMessage,
                    errorHandlingPolicy.OnError,
                    OnCriticalError,
                    TransportTransactionMode.ReceiveOnly);

                queueIngestor = transportInfrastructure.Receivers[inputEndpoint];

                await auditIngestor.VerifyCanReachForwardingAddress(cancellationToken);

                await queueIngestor.StartReceive(cancellationToken);

                logger.Info("Ensure started. Infrastructure started");
            }
            catch
            {
                if (queueIngestor != null)
                {
                    try
                    {
                        await queueIngestor.StopReceive(cancellationToken);
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                    {
                        // ignored
                    }
                }

                queueIngestor = null; // Setting to null so that it doesn't exit when it retries in line 185

                throw;
            }
            finally
            {
                logger.Debug("Ensure started. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Debug("Ensure started. Start/stop semaphore released");
            }
        }

        async Task EnsureStopped(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Info("Shutting down. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken);
                logger.Info("Shutting down. Start/stop semaphore acquired");

                if (queueIngestor == null)
                {
                    logger.Info("Shutting down. Already stopped, skipping shut down");
                    return; //Already stopped
                }

                var stoppable = queueIngestor;
                queueIngestor = null;
                logger.Info("Shutting down. Infrastructure shut down commencing");
                await stoppable.StopReceive(cancellationToken);
                logger.Info("Shutting down. Infrastructure shut down completed");
            }
            catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
            {
                // ignored
            }
            finally
            {
                logger.Info("Shutting down. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Info("Shutting down. Start/stop semaphore released");
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

            receivedAudits.Add(1);

            await channel.Writer.WriteAsync(messageContext, cancellationToken);
            await taskCompletionSource.Task;
        }

        async Task Loop(CancellationToken cancellationToken)
        {
            try
            {
                var contexts = new List<MessageContext>(transportSettings.MaxConcurrency.Value);

                long sequentialFailureCount = 0;
                DateTime lastSuccess = DateTime.MinValue;

                while (await channel.Reader.WaitToReadAsync(cancellationToken))
                {
                    var sw = Stopwatch.StartNew();
                    // TODO: Add timeout handling, if processing takes over for example 1 minute
                    // will only enter here if there is something to read.
                    try
                    {
                        // as long as there is something to read this will fetch up to MaximumConcurrency items
                        while (channel.Reader.TryRead(out var context))
                        {
                            contexts.Add(context);
                            auditMessageSize.Record(context.Body.Length / 1024D);
                        }

                        auditBatchSize.Record(contexts.Count);

                        await auditIngestor.Ingest(contexts, cancellationToken);
                        auditBatchDuration.Record(sw.ElapsedMilliseconds);

                        // No locking for consistency needed, just write, don't care about multi-threading
                        sequentialFailureCount = 0;
                        lastSuccess = DateTime.UtcNow;
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                    {
                        logger.Debug("Cancelled by host");
                        return; // No point in continueing as WaitToReadAsync will throw OCE
                    }
                    catch (Exception e) // show must go on
                    {
                        Interlocked.Increment(ref sequentialFailureCount);
                        logger.Warn($"Batch processing failed [#{sequentialFailureCount} @{lastSuccess:O}] ", e);

                        // Signal circuitbreaker, throttle whatever

                        // signal all message handling tasks to terminate
                        foreach (var context in contexts)
                        {
                            if (!context.GetTaskCompletionSource().TrySetException(e))
                            {
                                logger.Error("Loop TrySetException failed");
                            }
                        }
                    }
                    finally
                    {
                        const int infoThreshold = 5000;
                        const int warnThreshold = 15000;
                        const int errorThreshold = 60000;
                        var elapsed = sw.ElapsedMilliseconds;

                        if (elapsed > errorThreshold)
                        {
                            logger.ErrorFormat("Processing duration {0} exceeded {1}", elapsed, errorThreshold);
                        }
                        else if (elapsed > warnThreshold)
                        {
                            logger.WarnFormat("Processing duration {0} exceeded {1}", elapsed, warnThreshold);
                        }
                        else if (elapsed > infoThreshold)
                        {
                            logger.InfoFormat("Processing duration {0} exceeded {1}", elapsed, infoThreshold);
                        }

                        contexts.Clear();
                    }
                }
                // will fall out here when writer is completed
            }
            catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
            {
                logger.Debug("Cancelled by host");
            }
            catch (Exception e)
            {
                // Might the next exception scope throw an exception, consider this fatal as that cannot be an OCE
                logger.Fatal("Loop interrupted", e);
                applicationLifetime.StopApplication();
                throw;
            }
        }

        TransportInfrastructure transportInfrastructure;
        IMessageReceiver queueIngestor;
        Task ingestionWorker;

        readonly SemaphoreSlim startStopSemaphore = new(1);
        readonly string inputEndpoint;
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly AuditIngestor auditIngestor;
        readonly AuditIngestionFaultPolicy errorHandlingPolicy;
        readonly IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Settings settings;
        readonly Channel<MessageContext> channel;
        readonly Histogram<long> auditBatchSize = AuditMetrics.Meter.CreateHistogram<long>($"{AuditMetrics.Prefix}.batch_size_audits");
        readonly Histogram<double> auditBatchDuration = AuditMetrics.Meter.CreateHistogram<double>($"{AuditMetrics.Prefix}.batch_duration_audits", unit: "ms");
        readonly Histogram<double> auditMessageSize = AuditMetrics.Meter.CreateHistogram<double>($"{AuditMetrics.Prefix}.audit_message_size", unit: "kilobytes");
        readonly Counter<long> receivedAudits = AuditMetrics.Meter.CreateCounter<long>($"{AuditMetrics.Prefix}.received_audits");
        readonly Watchdog watchdog;
        readonly IHostApplicationLifetime applicationLifetime;

        CancellationTokenSource stopSource;

        static readonly ILog logger = LogManager.GetLogger<AuditIngestion>();
    }
}