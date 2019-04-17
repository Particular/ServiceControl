namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
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
            using (var session = Store.OpenAsyncSession())
            {
                failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(message.FailedMessageId))
                    .ConfigureAwait(false);
                //TODO check for failedMessage.Status = FailedMessageStatus.RetryIssued?

                if (failedMessage == null)
                {
                    return;
                }

                // mark the message as retried
                // todo: introduce a new state?
                // todo do we need to prevent regular retries?
                failedMessage.Status = FailedMessageStatus.RetryIssued;
                // todo: complete "original" message when edited message completes.

                //TODO do we need to prefix the id?
                var edit = await session.LoadAsync<FailedMessageEdit>(message.FailedMessageId)
                    .ConfigureAwait(false);
                if (edit == null)
                {
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
                    return;
                }
                
                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            
            // send
            var attempt = failedMessage.ProcessingAttempts.Last();
            var headers = new Dictionary<string, string>(attempt.Headers);
            // remove other headers
            // set new message id
            // add additional header representing the edit

            //TODO not accessible here
            //var messageId = CombGuid.Generate();

            var messageId = Guid.NewGuid().ToString("N");
            var body = Convert.FromBase64String(message.NewBody);
            var outgoingMessage = new OutgoingMessage(messageId, headers, body);
            AddressTag destination = new UnicastAddressTag(attempt.FailureDetails.AddressOfFailingEndpoint);

            //                var redirect = redirects[addressOfFailingEndpoint];
            //                if (redirect != null)
            //                {
            //                    addressOfFailingEndpoint = redirect.ToPhysicalAddress;
            //                }

            //TODO: can we use the incoming transaction?
            await Dispatcher.Dispatch(
                    new TransportOperations(new TransportOperation(outgoingMessage, destination)),
                    new TransportTransaction(),
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
    }

    class FailedMessageEdit
    {
        public string FailedMessageId { get; set; }
        public string EditId { get; set; }
    }
}
