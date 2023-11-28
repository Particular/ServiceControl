﻿namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ImportFailedErrors
    {
        public ImportFailedErrors(
            IFailedErrorImportDataStore store,
            ErrorIngestor errorIngestor,
            Settings settings,
            ITransportCustomization transportCustomization,
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
            var dispatcher = await transportCustomization.InitializeDispatcher("ImportFailedErrors", transportSettings);

            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(dispatcher);
            }

            await store.ProcessFailedErrorImports(async transportMessage =>
            {
                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, settings.ErrorQueue, EmptyContextBag);
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
        readonly ITransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}