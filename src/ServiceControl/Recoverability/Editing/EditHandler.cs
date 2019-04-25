namespace ServiceControl.Recoverability.Editing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using MessageRedirects;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public class EditHandler : IHandleMessages<EditAndSend>
    {
        public EditHandler(IDocumentStore store, IDispatchMessages dispatcher)
        {
            this.store = store;
            this.dispatcher = dispatcher;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName);
        }

        public async Task Handle(EditAndSend message, IMessageHandlerContext context)
        {
            FailedMessage failedMessage;
            MessageRedirectsCollection redirects;
            using (var session = store.OpenAsyncSession())
            {
                failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(message.FailedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    log.WarnFormat("Discarding edit {0} because no message failure for id {1} has been found.", context.MessageId, message.FailedMessageId);
                    return;
                }

                var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(message.FailedMessageId))
                    .ConfigureAwait(false);
                if (edit == null)
                {
                    if (failedMessage.Status != FailedMessageStatus.Unresolved)
                    {
                        log.WarnFormat("Discarding edit {0} because message failure {1} doesn't have state 'Unresolved'.", context.MessageId, message.FailedMessageId);
                        return;
                    }

                    // create a retries document to prevent concurrent edits
                    await session.StoreAsync(new FailedMessageEdit
                    {
                        Id = FailedMessageEdit.MakeDocumentId(message.FailedMessageId),
                        FailedMessageId = message.FailedMessageId,
                        EditId = context.MessageId
                    }, Etag.Empty).ConfigureAwait(false);
                }
                else if (edit.EditId != context.MessageId)
                {
                    log.WarnFormat("Discarding edit {0} because the failure ({1}) has already been edited by edit {2}", context.MessageId, message.FailedMessageId, edit.EditId);
                    return;
                }

                // the original failure is marked as resolved as any failures of the edited message are treated as a new message failure.
                failedMessage.Status = FailedMessageStatus.Resolved;

                redirects = await MessageRedirectsCollection.GetOrCreate(session)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            var attempt = failedMessage.ProcessingAttempts.Last();

            var outgoingMessage = BuildMessage(message);
            // mark the new message with a link to the original message id
            outgoingMessage.Headers.Add("ServiceControl.EditOf", attempt.MessageId);
            var address = ApplyRedirect(attempt.FailureDetails.AddressOfFailingEndpoint, redirects);
            await DispatchEditedMessage(outgoingMessage, address, context)
                .ConfigureAwait(false);
        }

        OutgoingMessage BuildMessage(EditAndSend message)
        {
            var messageId = CombGuid.Generate().ToString();
            var headers = HeaderFilter.RemoveErrorMessageHeaders(message.NewHeaders);
            corruptedReplyToHeaderStrategy.FixCorruptedReplyToHeader(headers);
            headers[Headers.MessageId] = Guid.NewGuid().ToString("D");

            var body = Convert.FromBase64String(message.NewBody);
            var outgoingMessage = new OutgoingMessage(messageId, headers, body);
            return outgoingMessage;
        }

        static string ApplyRedirect(string addressOfFailingEndpoint, MessageRedirectsCollection redirects)
        {
            var redirect = redirects[addressOfFailingEndpoint];
            if (redirect != null)
            {
                addressOfFailingEndpoint = redirect.ToPhysicalAddress;
            }

            return addressOfFailingEndpoint;
        }

        Task DispatchEditedMessage(OutgoingMessage editedMessage, string address, IMessageHandlerContext context)
        {
            AddressTag destination = new UnicastAddressTag(address);
            var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();

            return dispatcher.Dispatch(
                new TransportOperations(new TransportOperation(editedMessage, destination)),
                transportTransaction,
                new ContextBag());
        }

        CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        IDocumentStore store;
        IDispatchMessages dispatcher;
        static ILog log = LogManager.GetLogger<EditHandler>();
    }
}