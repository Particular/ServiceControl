namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    class AuditIngestion
    {
        public AuditIngestion(
            Func<MessageContext, IDispatchMessages, Task> onMessage, 
            Func<IDispatchMessages, Task> initialize,
            string inputEndpoint, 
            RawEndpointFactory rawEndpointFactory,
            IErrorHandlingPolicy errorHandlingPolicy,
            Func<string, Exception, Task> onCriticalError)
        {
            this.onMessage = onMessage;
            this.initialize = initialize;
            this.inputEndpoint = inputEndpoint;
            this.rawEndpointFactory = rawEndpointFactory;
            this.errorHandlingPolicy = errorHandlingPolicy;
            this.onCriticalError = onCriticalError;
        }

        public async Task EnsureStarted(CancellationToken cancellationToken)
        {
            try
            {
                logger.Debug("Ensure started. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Debug("Ensure started. Start/stop semaphore acquired");

                if (ingestionEndpoint != null)
                {
                    logger.Debug("Ensure started. Already started, skipping start up");
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateAuditIngestor(inputEndpoint, onMessage);

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(errorHandlingPolicy);

                logger.Info("Ensure started. Infrastructure starting");
                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                await initialize(startableRaw).ConfigureAwait(false);

                ingestionEndpoint = await startableRaw.Start()
                    .ConfigureAwait(false);
                logger.Info("Ensure started. Infrastructure started");
            }
            finally
            {
                logger.Debug("Ensure started. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Debug("Ensure started. Start/stop semaphore released");
            }
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx) => onCriticalError(ctx.Error, ctx.Exception);

        public async Task EnsureStopped(CancellationToken cancellationToken)
        {
            try
            {
                logger.Info("Shutting down. Start/stop semaphore acquiring");
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                logger.Info("Shutting down. Start/stop semaphore acquired");

                if (ingestionEndpoint == null)
                {
                    logger.Info("Shutting down. Already stopped, skipping shut down");
                    return; //Already stopped
                }
                var stoppable = ingestionEndpoint;
                ingestionEndpoint = null;
                logger.Info("Shutting down. Infrastructure shut down commencing");
                await stoppable.Stop().ConfigureAwait(false);
                logger.Info("Shutting down. Infrastructure shut down completed");
            }
            finally
            {
                logger.Info("Shutting down. Start/stop semaphore releasing");
                startStopSemaphore.Release();
                logger.Info("Shutting down. Start/stop semaphore released");
            }
        }

        SemaphoreSlim startStopSemaphore = new SemaphoreSlim(1);
        Func<MessageContext, IDispatchMessages, Task> onMessage;
        Func<IDispatchMessages, Task> initialize;
        string inputEndpoint;
        RawEndpointFactory rawEndpointFactory;
        IErrorHandlingPolicy errorHandlingPolicy;
        Func<string, Exception, Task> onCriticalError;
        IReceivingRawEndpoint ingestionEndpoint;

        static readonly ILog logger = LogManager.GetLogger<AuditIngestion>();
    }
}