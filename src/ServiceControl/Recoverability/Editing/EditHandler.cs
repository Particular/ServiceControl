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
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public class EditHandler : IHandleMessages<EditAndSend>
    {
        static ILog log = LogManager.GetLogger<EditHandler>();
        public IDocumentStore Store { get; set; }
        public IDispatchMessages Dispatcher { get; set; }

        public async Task Handle(EditAndSend message, IMessageHandlerContext context)
        {
            FailedMessage failedMessage;
            MessageRedirectsCollection redirects;
            using (var session = Store.OpenAsyncSession())
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

            var messageId = CombGuid.Generate().ToString();
            var attempt = failedMessage.ProcessingAttempts.Last();
            var headers = HeaderFilter.RemoveErrorMessageHeaders(attempt.Headers);
            headers[Headers.MessageId] = Guid.NewGuid().ToString("D");
            headers.Add("ServiceControl.EditOf", attempt.MessageId);

            //TODO: do we need the CorruptedReplyToHeaderStrategy as well?

            var body = Convert.FromBase64String(message.NewBody);
            var outgoingMessage = new OutgoingMessage(messageId, headers, body);

            var destination = CreateDestinationAddress(attempt, redirects);

            var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();
            await Dispatcher.Dispatch(
                    new TransportOperations(new TransportOperation(outgoingMessage, destination)),
                    transportTransaction,
                    new ContextBag())
                .ConfigureAwait(false);
        }

        static AddressTag CreateDestinationAddress(FailedMessage.ProcessingAttempt attempt, MessageRedirectsCollection redirects)
        {
            var addressOfFailingEndpoint = attempt.FailureDetails.AddressOfFailingEndpoint;
            var redirect = redirects[addressOfFailingEndpoint];
            if (redirect != null)
            {
                addressOfFailingEndpoint = redirect.ToPhysicalAddress;
            }

            AddressTag destination = new UnicastAddressTag(addressOfFailingEndpoint);
            return destination;
        }
    }


}