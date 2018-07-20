namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Raven.Client;

    class ReclassifyErrorsHandler : IHandleMessages<ReclassifyErrors>
    {
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

        IDomainEvents domainEvents;
        IDocumentStore store;
        IEnumerable<IFailureClassifier> classifiers;
        Reclassifier reclassifier;
        static int executing;
    }
}