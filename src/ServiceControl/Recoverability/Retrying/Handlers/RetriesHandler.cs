namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
    using MessageRedirects;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailed>,
        IHandleMessages<MessageFailedRepeatedly>,
        IHandleMessages<RetryMessagesByQueueAddress>,
        IHandleMessages<RetryWithModifications>
    {
        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public IDocumentStore Store { get; set; }

        public IDispatchMessages Dispatcher { get; set; }

        /// <summary>
        /// For handling leftover messages. MessageFailed are no longer published on the bus and the code is moved to <see cref="FailedMessageRetryCleaner"/>.
        /// </summary>
        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            if (message.RepeatedFailure)
            {
                return RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.FromResult(0);
        }

        public Task Handle(MessageFailedRepeatedly message, IMessageHandlerContext context)
        {
            return RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
        }

        public Task Handle(RequestRetryAll message, IMessageHandlerContext context)
        {
            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(message.Endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            }

            return Task.FromResult(0);
        }

        public Task Handle(RetryMessage message, IMessageHandlerContext context)
        {
            return Retries.StartRetryForSingleMessage(message.FailedMessageId);
        }

        public async Task Handle(RetryWithModifications message, IMessageHandlerContext context)
        {
            FailedMessage failedMessage;
            MessageRedirectsCollection redirects;
            using (var session = Store.OpenAsyncSession())
            {
                failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(message.FailedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    //TODO: log
                    return;
                }

                //TODO do we need to prefix the id?
                var edit = await session.LoadAsync<FailedMessageEdit>(message.FailedMessageId)
                    .ConfigureAwait(false);
                if (edit == null)
                {
                    if (failedMessage.Status != FailedMessageStatus.Unresolved)
                    {
                        //TODO log
                        return;
                    }

                    // create a retries document to prevent concurrent edits
                    await session.StoreAsync(new FailedMessageEdit
                    {
                        FailedMessageId = message.FailedMessageId,
                        EditId = context.MessageId
                    }, Etag.Empty, message.FailedMessageId).ConfigureAwait(false);
                }
                else if(edit.EditId != context.MessageId)
                {
                    // this is a concurrent edit -> discard
                    //TODO log
                    return;
                }

                // todo: introduce a new state?
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

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context)
        {
            return Retries.StartRetryForMessageSelection(message.MessageUniqueIds);
        }

        public Task Handle(RetryMessagesByQueueAddress message, IMessageHandlerContext context)
        {
            var failedQueueAddress = message.QueueAddress;

            Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == message.Status, $"all messages for failed queue address '{message.QueueAddress}'");

            return Task.FromResult(0);
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

    class FailedMessageEdit
    {
        public string FailedMessageId { get; set; }
        public string EditId { get; set; }
    }
}
