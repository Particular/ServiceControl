namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
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
            TransportSettings transportSettings,
            ReceiveAddresses receiveAddresses)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.settings = settings;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.receiveAddresses = receiveAddresses;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var dispatcher = await transportCustomization.InitializeDispatcher("ImportFailedErrors", transportSettings);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(dispatcher);
            }

            await store.ProcessFailedErrorImports(async transportMessage =>
            {
                //TODO decent chance adding ReceiveAddresses here isn't correct, but wasn't clear how else to get the info since it's passed to the MessagePump ctor
                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, receiveAddresses.MainReceiveAddress, EmptyContextBag);
                var taskCompletionSource =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                messageContext.SetTaskCompletionSource(taskCompletionSource);

                await errorIngestor.Ingest(new List<MessageContext> { messageContext }, dispatcher);
                await taskCompletionSource.Task;
            }, cancellationToken);
        }

        readonly IFailedErrorImportDataStore store;
        readonly ErrorIngestor errorIngestor;
        readonly Settings settings;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly ReceiveAddresses receiveAddresses;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}