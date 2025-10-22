namespace ServiceControl.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ImportFailedErrors(
        IFailedErrorImportDataStore store,
        ErrorIngestor errorIngestor,
        IOptions<Settings> settingsOptions
    )
    {
        public async Task Run(CancellationToken cancellationToken = default)
        {
            var settings = settingsOptions.Value;
            if (settings.ServiceControl.ForwardErrorMessages == true)
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
                    settings.ServiceBus.ErrorQueue,
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