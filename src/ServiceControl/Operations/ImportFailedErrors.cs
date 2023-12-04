namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class ImportFailedErrors
    {
        public ImportFailedErrors(
            IFailedErrorImportDataStore store,
            ErrorIngestor errorIngestor,
            Settings settings)
        {
            this.store = store;
            this.errorIngestor = errorIngestor;
            this.settings = settings;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress();
            }

            await store.ProcessFailedErrorImports(async transportMessage =>
            {
                var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, settings.ErrorQueue, EmptyContextBag);
                var taskCompletionSource =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                messageContext.SetTaskCompletionSource(taskCompletionSource);

                await errorIngestor.Ingest(new List<MessageContext> { messageContext });
                await taskCompletionSource.Task;
            }, cancellationToken);
        }

        readonly IFailedErrorImportDataStore store;
        readonly ErrorIngestor errorIngestor;
        readonly Settings settings;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}