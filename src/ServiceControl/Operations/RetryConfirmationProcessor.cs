namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using MessageFailures;
    using NServiceBus.Transport;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Recoverability;

    class RetryConfirmationProcessor
    {
        public const string SuccessfulRetryHeader = "ServiceControl.Retry.Successful";
        const string RetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";

        public RetryConfirmationProcessor(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public IReadOnlyCollection<ICommandData> Process(List<MessageContext> contexts)
        {
            var allCommands = new List<ICommandData>(contexts.Count * 2);

            foreach (var context in contexts)
            {
                var commands = CreateDatabaseCommands(context);
                allCommands.AddRange(commands);
            }

            return allCommands;
        }

        public Task Announce(MessageContext messageContext)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                FailedMessageId = messageContext.Headers[RetryUniqueMessageIdHeader],
            });
        }

        static IEnumerable<ICommandData> CreateDatabaseCommands(MessageContext context)
        {
            var retriedMessageUniqueId = context.Headers[RetryUniqueMessageIdHeader];
            var failedMessageDocumentId = FailedMessage.MakeDocumentId(retriedMessageUniqueId);
            var failedMessageRetryDocumentId = FailedMessageRetry.MakeDocumentId(retriedMessageUniqueId);

            var patchCommand = new PatchCommandData
            {
                Key = failedMessageDocumentId,
                Patches = new[]
                {
                    new PatchRequest {Type = PatchCommandType.Set, Name = nameof(FailedMessage.Status), Value = (int)FailedMessageStatus.Resolved}
                }
            };

            var deleteCommand = new DeleteCommandData { Key = failedMessageRetryDocumentId, };
            yield return patchCommand;
            yield return deleteCommand;
        }

        readonly IDomainEvents domainEvents;
    }
}