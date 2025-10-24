namespace ServiceControl.Recoverability.Editing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.MessageRedirects;

    class EditHandler : IHandleMessages<EditAndSend>
    {
        public EditHandler(IErrorMessageDataStore store, IMessageRedirectsDataStore redirectsStore, IMessageDispatcher dispatcher, ErrorQueueNameCache errorQueueNameCache, ILogger<EditHandler> logger)
        {
            this.store = store;
            this.redirectsStore = redirectsStore;
            this.dispatcher = dispatcher;
            this.errorQueueNameCache = errorQueueNameCache;
            this.logger = logger;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName, logger);

        }

        public async Task Handle(EditAndSend message, IMessageHandlerContext context)
        {
            string editRequestIdentifier = context.MessageId;
            FailedMessage failedMessage;
            using (var session = await store.CreateEditFailedMessageManager())
            {
                failedMessage = await session.GetFailedMessage(message.FailedMessageId);

                if (failedMessage == null)
                {
                    logger.LogWarning("Discarding edit {MessageId} because no message failure for id {FailedMessageId} has been found", context.MessageId, message.FailedMessageId);
                    return;
                }

                var editId = await session.GetCurrentEditingRequestId(message.FailedMessageId);
                if (editId == null)
                {
                    if (failedMessage.Status != FailedMessageStatus.Unresolved)
                    {
                        logger.LogWarning("Discarding edit {MessageId} because message failure {FailedMessageId} doesn't have state 'Unresolved'", context.MessageId, message.FailedMessageId);
                        return;
                    }

                    // create a retries document to prevent concurrent edits
                    await session.SetCurrentEditingRequestId(editRequestIdentifier);
                }
                else if (editId != editRequestIdentifier)
                {
                    logger.LogWarning("Discarding edit & retry request because the failed message id {FailedMessageId} has already been edited by Message ID {EditedMessageId}", message.FailedMessageId, editId);
                    return;
                }

                // the original failure is marked as resolved as any failures of the edited message are treated as a new message failure.
                await session.SetFailedMessageAsResolved();


                await session.SaveChanges();
            }

            var redirects = await redirectsStore.GetOrCreate();

            var attempt = failedMessage.ProcessingAttempts.Last();

            var outgoingMessage = BuildMessage(message);
            // mark the new message with a link to the original message id
            outgoingMessage.Headers.Add("ServiceControl.EditOf", message.FailedMessageId);
            outgoingMessage.Headers["ServiceControl.Retry.AcknowledgementQueue"] = errorQueueNameCache.ResolvedErrorAddress;
            outgoingMessage.Headers["ServiceControl.Retry.UniqueMessageId"] = editRequestIdentifier; //lets re-use the edit request identifier as the value for ServiceControl.Retry.UniqueMessageId so that when the notification from NSB about successful retries arrives, we can use this Id to query FailedMessageEdit by EditId  

            var address = ApplyRedirect(attempt.FailureDetails.AddressOfFailingEndpoint, redirects);

            if (outgoingMessage.Headers.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                outgoingMessage.Headers["ServiceControl.TargetEndpointAddress"] = address;
                address = retryTo;
            }
            await DispatchEditedMessage(outgoingMessage, address, context);
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
                context.CancellationToken);
        }

        readonly CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        readonly IErrorMessageDataStore store;
        readonly IMessageRedirectsDataStore redirectsStore;
        readonly IMessageDispatcher dispatcher;
        readonly ErrorQueueNameCache errorQueueNameCache;
        readonly ILogger<EditHandler> logger;
    }
}