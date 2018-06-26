namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures.InternalMessages;

    class ReclassifyErrorsHandler : IHandleMessages<ReclassifyErrors>
    {
        IDomainEvents domainEvents;
        IDocumentStore store;
        IEnumerable<IFailureClassifier> classifiers;
        Reclassifier reclassifier;
        static int executing;

        public ReclassifyErrorsHandler(IDocumentStore store, IDomainEvents domainEvents, ShutdownNotifier notifier, IEnumerable<IFailureClassifier> classifiers)
        {
            this.store = store;
            this.classifiers = classifiers;
            this.domainEvents = domainEvents;

            reclassifier = new Reclassifier(notifier);
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
                var failedMessagesReclassified = await reclassifier.ReclassifyFailedMessages(store, message.Force, classifiers)
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
    }
}