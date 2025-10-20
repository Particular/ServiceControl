namespace ServiceControl.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ImportFailedErrors(
        IFailedErrorImportDataStore store,
        ErrorIngestor errorIngestor,
        Settings settings)
    {
        public async Task Run(CancellationToken cancellationToken = default)
        {
            if (settings.ForwardErrorMessages == true)
            {
                await errorIngestor.VerifyCanReachForwardingAddress(cancellationToken);
            }

            await store.ProcessFailedErrorImports(async transportMessage =>
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

                await errorIngestor.Ingest([messageContext], cancellationToken);
                await taskCompletionSource.Task;
            }, cancellationToken);
        }

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
    }
}