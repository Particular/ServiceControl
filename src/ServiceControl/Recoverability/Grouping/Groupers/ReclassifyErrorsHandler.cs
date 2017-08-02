namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures.InternalMessages;

    class ReclassifyErrorsHandler : IHandleMessages<ReclassifyErrors>
    {
        readonly IBus bus;
        readonly IDocumentStore store;
        readonly IEnumerable<IFailureClassifier> classifiers;
        readonly Reclassifier reclassifier;
        private static int executing;

        ILog logger = LogManager.GetLogger<ReclassifyErrorsHandler>();

        public ReclassifyErrorsHandler(IBus bus, IDocumentStore store, ShutdownNotifier notifier, IEnumerable<IFailureClassifier> classifiers)
        {
            this.bus = bus;
            this.store = store;
            this.classifiers = classifiers;

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
                    bus.Publish(new ReclassificationOfErrorMessageComplete
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