namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Transports;

    class ImportFailedAudits
    {
        public ImportFailedAudits(
            IFailedAuditStorage failedAuditStore,
            AuditIngestor auditIngestor,
            TransportCustomization transportCustomization,
            TransportSettings transportSettings,
            ReceiveAddresses receiveAddresses)
        {
            this.failedAuditStore = failedAuditStore;
            this.auditIngestor = auditIngestor;
            this.transportCustomization = transportCustomization;
            this.transportSettings = transportSettings;
            this.receiveAddresses = receiveAddresses;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            var dispatcher = await transportCustomization.InitializeDispatcher("ImportFailedAudits", transportSettings);

            await auditIngestor.VerifyCanReachForwardingAddress(dispatcher);

            var succeeded = 0;
            var failed = 0;

            await failedAuditStore.ProcessFailedMessages(
                async (transportMessage, markComplete, token) =>
                    {
                        try
                        {
                            //TODO decent chance adding ReceiveAddresses here isn't correct, but wasn't clear how else to get the info since it's passed to the MessagePump ctor
                            var messageContext = new MessageContext(transportMessage.Id, transportMessage.Headers, transportMessage.Body, EmptyTransaction, receiveAddresses.MainReceiveAddress, EmptyContextBag);
                            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            messageContext.SetTaskCompletionSource(taskCompletionSource);

                            await auditIngestor.Ingest(new List<MessageContext> { messageContext }, dispatcher);

                            await taskCompletionSource.Task;

                            await markComplete(token);
                            succeeded++;
                            if (Logger.IsDebugEnabled)
                            {
                                Logger.Debug($"Successfully re-imported failed audit message {transportMessage.Id}.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // no-op
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while attempting to re-import failed audit message {transportMessage.Id}.", e);
                            failed++;
                        }

                    }, cancellationToken);

            Logger.Info($"Done re-importing failed audits. Successfully re-imported {succeeded} messages. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages.");
            }
        }

        readonly IFailedAuditStorage failedAuditStore;
        readonly AuditIngestor auditIngestor;
        readonly TransportCustomization transportCustomization;
        readonly TransportSettings transportSettings;
        readonly ReceiveAddresses receiveAddresses;

        static readonly TransportTransaction EmptyTransaction = new TransportTransaction();
        static readonly ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }
}