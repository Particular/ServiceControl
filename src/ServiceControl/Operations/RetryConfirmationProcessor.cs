namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Recoverability;

    class RetryConfirmationProcessor
    {
        public const string SuccessfulRetryHeader = "ServiceControl.Retry.SuccessfulRetryUniqueMessageId";

        public RetryConfirmationProcessor(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public (IReadOnlyList<MessageContext>, IReadOnlyCollection<ICommandData>) Process(List<MessageContext> contexts)
        {
            var storedContexts = new List<MessageContext>(contexts.Count);
            var allCommands = new List<ICommandData>(contexts.Count);

            foreach (var context in contexts)
            {
                try
                {
                    var commands = ProcessOne(context);
                    allCommands.AddRange(commands);
                    storedContexts.Add(context);
                }
                catch (Exception e)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Processing of message '{context.MessageId}' failed.", e);
                    }

                    context.GetTaskCompletionSource().TrySetException(e);
                }
            }

            return (storedContexts, allCommands);
        }

        public Task Announce(MessageContext messageContext)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                FailedMessageId = messageContext.Headers[SuccessfulRetryHeader]
            });
        }

        static IEnumerable<ICommandData> ProcessOne(MessageContext context)
        {
            var retriedMessageUniqueId = context.Headers[SuccessfulRetryHeader];
            var failedMessageDocumentId = FailedMessage.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            var patchCommand = new PatchCommandData
            {
                Key = failedMessageDocumentId,
                Patches = new[]
                {
                    new PatchRequest {Type = PatchCommandType.Set, Name = "status", Value = (int)FailedMessageStatus.Resolved}
                }
            };

            var deleteCommand = new DeleteCommandData { Key = failedMessageRetryDocumentId, };
            yield return patchCommand;
            yield return deleteCommand;
        }

        static readonly ILog Logger = LogManager.GetLogger<RetryConfirmationProcessor>();
        readonly IDomainEvents domainEvents;
    }
}