namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
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
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint != null)
                {
                    return; //Already started
                }

                var rawConfiguration = rawEndpointFactory.CreateRawEndpointConfiguration(inputEndpoint, onMessage);

                rawConfiguration.Settings.Set("onCriticalErrorAction", (Func<ICriticalErrorContext, Task>)OnCriticalErrorAction);

                rawConfiguration.CustomErrorHandlingPolicy(errorHandlingPolicy);

                var startableRaw = await RawEndpoint.Create(rawConfiguration).ConfigureAwait(false);

                await initialize(startableRaw).ConfigureAwait(false);

                ingestionEndpoint = await startableRaw.Start()
                    .ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
            }
        }

        Task OnCriticalErrorAction(ICriticalErrorContext ctx) => onCriticalError(ctx.Error, ctx.Exception);

        public async Task EnsureStopped(CancellationToken cancellationToken)
        {
            try
            {
                await startStopSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (ingestionEndpoint == null)
                {
                    return; //Already stopped
                }
                var stoppable = ingestionEndpoint;
                ingestionEndpoint = null;
                await stoppable.Stop().ConfigureAwait(false);
            }
            finally
            {
                startStopSemaphore.Release();
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
    }
}