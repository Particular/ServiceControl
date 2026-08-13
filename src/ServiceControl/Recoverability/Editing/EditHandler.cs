namespace ServiceControl.Recoverability.Editing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.Auth;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.MessageRedirects;

    [Handler]
    class EditHandler(IEditFailedMessagesDataStore store, IMessageRedirectsDataStore redirectsStore, IMessageDispatcher dispatcher, ErrorQueueNameCache errorQueueNameCache, IDomainEvents domainEvents, IMessageActionAuditLog auditLog, ILogger<EditHandler> logger)
        : IHandleMessages<EditAndSend>
    {
        public async Task Handle(EditAndSend message, IMessageHandlerContext context)
        {
            var beginEdit = await store.TryBeginEdit(message.FailedMessageId, context.MessageId, context.CancellationToken);

            switch (beginEdit.Outcome)
            {
                case BeginEditOutcome.MessageNotFound:
                    logger.LogWarning("Discarding edit {MessageId} because no message failure for id {FailedMessageId} has been found", context.MessageId, message.FailedMessageId);
                    return;
                case BeginEditOutcome.MessageNotUnresolved:
                    logger.LogWarning("Discarding edit {MessageId} because message failure {FailedMessageId} doesn't have state 'Unresolved'", context.MessageId, message.FailedMessageId);
                    return;
                case BeginEditOutcome.AcquiredByAnotherEdit:
                    logger.LogWarning("Discarding edit & retry request because the failed message id {FailedMessageId} has already been edited by Message ID {EditedMessageId}", message.FailedMessageId, beginEdit.ExistingEditId);
                    return;
                case BeginEditOutcome.Acquired:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown begin-edit outcome: {beginEdit.Outcome}");
            }

            // The store commits resolution of the original failure before returning. Any failure
            // of the edited message is therefore treated as a new failure, preserving the existing
            // resolve-before-dispatch behavior.
            var failedMessage = beginEdit.FailedMessage!;

            var redirects = await redirectsStore.GetRedirects(context.CancellationToken);

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
            await DispatchEditedMessage(outgoingMessage, address, context);

            // Audited only after the edited message is really dispatched. A dispatch failure is
            // redelivered and dispatches again, so each audit entry matches an actual dispatch.
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            if (!string.IsNullOrEmpty(operationId))
            {
                auditLog.MessageAction(user, MessageActionKind.Edit, Permissions.ErrorMessagesEdit, MessageActionScope.Single, message.FailedMessageId, operationId);
            }

            await domainEvents.Raise(new MessageEditedAndRetried
            {
                FailedMessageId = message.FailedMessageId
            }, context.CancellationToken);
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

        static string ApplyRedirect(string addressOfFailingEndpoint, IReadOnlyList<MessageRedirect> redirects)
        {
            var redirect = redirects.FindByAddress(addressOfFailingEndpoint);
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

        readonly CorruptedReplyToHeaderStrategy corruptedReplyToHeaderStrategy = new(RuntimeEnvironment.MachineName, logger);
    }
}