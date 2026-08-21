namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Ingestion;

    public class ImportFailedErrors(
        IFailedErrorImportDataStore store,
        ErrorIngestor errorIngestor,
        Lazy<IMessageDispatcher> messageDispatcher,
        Settings settings)
    {
        public async Task Run(CancellationToken cancellationToken = default)
        {
            if (settings.ForwardErrorMessages)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(messageDispatcher.Value, cancellationToken);
            }

            await store.ProcessFailedErrorImports(async (transportMessage, token) =>
            {
                var messageContext = new MessageContext(
                    transportMessage.Id,
                    transportMessage.Headers,
                    transportMessage.Body,
                    EmptyTransaction,
                    settings.ErrorQueue,
                    EmptyContextBag
                );
                var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                messageContext.SetTaskCompletionSource(taskCompletionSource);

                await errorIngestor.Ingest([messageContext], messageDispatcher.Value, token);
                await taskCompletionSource.Task;
            }, cancellationToken);
        }

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}