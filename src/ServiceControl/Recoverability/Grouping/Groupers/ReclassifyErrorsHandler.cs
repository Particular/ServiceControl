namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using ServiceControl.Persistence;

    [Obsolete("Only used by legacy RavenDB35 storage engine")] // TODO: how to deal with this domain event
    class ReclassifyErrorsHandler : IHandleMessages<ReclassifyErrors>
    {
        public ReclassifyErrorsHandler(IReclassifyFailedMessages reclassifier, IDomainEvents domainEvents)
        {
            this.reclassifier = reclassifier;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(ReclassifyErrors message, IMessageHandlerContext context)
        {
            if (Interlocked.Exchange(ref executing, 1) != 0)
            {
                // Prevent more then one execution at a time
                return;
            }

            try
            {
                var failedMessagesReclassified = await reclassifier.ReclassifyFailedMessages(message.Force)
                    .ConfigureAwait(false);

                if (failedMessagesReclassified > 0)
                {
                    await domainEvents.Raise(new ReclassificationOfErrorMessageComplete
                    {
                        NumberofMessageReclassified = failedMessagesReclassified
                    }).ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Exchange(ref executing, 0);
            }
        }

        IDomainEvents domainEvents;
        IReclassifyFailedMessages reclassifier;
        static int executing;
    }
}