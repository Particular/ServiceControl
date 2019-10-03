namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;

    class HandleLegacyFailureResolvedByRetry : IHandleMessages<MessageFailureResolvedByRetry>
    {
        public HandleLegacyFailureResolvedByRetry(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        // This is only needed because we might get this from legacy not yet converted instances
        public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            return domainEvents.Raise(message);
        }

        readonly IDomainEvents domainEvents;
    }
}