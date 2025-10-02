namespace ServiceControl.Recoverability.Editing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.MessageRedirects;

    class EditHandlerCopy
    {
        public EditHandlerCopy(IErrorMessageDataStore store, IMessageRedirectsDataStore redirectsStore, IMessageDispatcher dispatcher, ErrorQueueNameCache errorQueueNameCache, ILogger<EditHandler> logger)
        {
            this.store = store;
            this.redirectsStore = redirectsStore;
            this.dispatcher = dispatcher;
            this.errorQueueNameCache = errorQueueNameCache;
            this.logger = logger;
            corruptedReplyToHeaderStrategy = new CorruptedReplyToHeaderStrategy(RuntimeEnvironment.MachineName, logger);

        }

        public async Task Handle(EditAndSend message, string retryAttemptId)
        {
            FailedMessage failedMessage;
            using (var session = await store.CreateEditFailedMessageManager())
            {
                failedMessage = await session.GetFailedMessage(message.FailedMessageId);

                if (failedMessage == null)
                {
                    logger.LogWarning("Discarding edit {MessageId} because no message failure for id {FailedMessageId} has been found", retryAttemptId, message.FailedMessageId);
                    return;
                }

                var editId = await session.GetCurrentEditingMessageId(message.FailedMessageId);
                if (editId == null)
                {
                    if (failedMessage.Status != FailedMessageStatus.Unresolved)
                    {
                        logger.LogWarning("Discarding edit {MessageId} because message failure {FailedMessageId} doesn't have state 'Unresolved'", retryAttemptId, message.FailedMessageId);
                        return;
                    }

                    // create a retries document to prevent concurrent edits
                    await session.SetCurrentEditingMessageId(retryAttemptId);
                }
                else if (editId != retryAttemptId)
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

            var address = ApplyRedirect(attempt.FailureDetails.AddressOfFailingEndpoint, redirects);

            if (outgoingMessage.Headers.TryGetValue("ServiceControl.RetryTo", out var retryTo))
            {
                outgoingMessage.Headers["ServiceControl.TargetEndpointAddress"] = address;
                address = retryTo;
            }
            await DispatchEditedMessage(outgoingMessage, address);
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

        Task DispatchEditedMessage(OutgoingMessage editedMessage, string address)
        {
            AddressTag destination = new UnicastAddressTag(address);
            //var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();

            //return dispatcher.Dispatch(
            //    new TransportOperations(new TransportOperation(editedMessage, destination)),
            //    transportTransaction,
            //    context.CancellationToken);


            return dispatcher.Dispatch(
                new TransportOperations(new TransportOperation(editedMessage, destination)), new TransportTransaction(), CancellationToken.None);
        }

        readonly CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy;
        readonly IErrorMessageDataStore store;
        readonly IMessageRedirectsDataStore redirectsStore;
        readonly IMessageDispatcher dispatcher;
        readonly ErrorQueueNameCache errorQueueNameCache;
        readonly ILogger<EditHandler> logger;
    }
}