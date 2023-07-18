namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Transports;

    class ImportFailedErrors
    {
        public ImportFailedErrors(
            IFailedErrorImportDataStore store,
            ErrorIngestor errorIngestor,
            Settings settings,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var dispatcher = await transportCustomization.InitializeDispatcher("ImportFailedErrors", transportSettings).ConfigureAwait(false);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(dispatcher).ConfigureAwait(false);
            }

            await store.ProcessFailedErrorImports(async transportMessage =>
            {
                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers,
                    transportMessage.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);
                var taskCompletionSource =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                messageContext.SetTaskCompletionSource(taskCompletionSource);

                await errorIngestor.Ingest(new List<MessageContext> {messageContext}, dispatcher).ConfigureAwait(false);
                await taskCompletionSource.Task.ConfigureAwait(false);
            }, cancellationToken)
                .ConfigureAwait(false);
        }

        readonly IFailedErrorImportDataStore store;
        readonly ErrorIngestor errorIngestor;
        readonly Settings settings;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}