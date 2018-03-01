namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Logging;
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

        ILog logger = LogManager.GetLogger<ReclassifyErrorsHandler>();

        public ReclassifyErrorsHandler(IDocumentStore store, IDomainEvents domainEvents, ShutdownNotifier notifier, IEnumerable<IFailureClassifier> classifiers)
        {
            this.store = store;
            this.classifiers = classifiers;
            this.domainEvents = domainEvents;

            reclassifier = new Reclassifier(notifier);
        }

        public void Handle(ReclassifyErrors message)
        {
            if (Interlocked.Exchange(ref executing, 1) != 0)
            {
                // Prevent more then one execution at a time
                return;
            }

            try
            {
                var failedMessagesReclassified = reclassifier.ReclassifyFailedMessages(store, message.Force, classifiers);

                if (failedMessagesReclassified > 0)
                {
                    domainEvents.Raise(new ReclassificationOfErrorMessageComplete
                    {
                        NumberofMessageReclassified = failedMessagesReclassified
                    });
                }
            }
            finally
            {
                Interlocked.Exchange(ref executing, 0);
            }
        }
    }
}