namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    class ReclassifyErrorsHandler : IHandleMessages<ReclassifyErrors>
    {
        readonly IBus bus;
        readonly IDocumentStore store;
        readonly IEnumerable<IFailureClassifier> classifiers;
        const int BatchSize = 1000;
        int failedMessagesReclassified;
        private bool abort;
        private static int executing;

        ILog logger = LogManager.GetLogger<ReclassifyErrorsHandler>();

        public ReclassifyErrorsHandler(IBus bus, IDocumentStore store, ShutdownNotifier notifier, IEnumerable<IFailureClassifier> classifiers)
        {
            this.bus = bus;
            this.store = store;
            this.classifiers = classifiers;

            notifier.Register(() => { abort = true; });
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
                using (var session = store.OpenSession())
                {
                    ReclassifyErrorSettings settings = null;

                    if (!message.Force)
                    {
                        settings = session.Load<ReclassifyErrorSettings>(ReclassifyErrorSettings.IdentifierCase);

                        if (settings != null && settings.ReclassificationDone)
                        {
                            logger.Info("Skipping reclassification of failures as classification has already been done.");
                            return;
                        }
                    }

                    logger.Info("Reclassification of failures started.");

                    var query = session.Query<FailedMessage, FailedMessageViewIndex>()
                        .Where(f => f.Status == FailedMessageStatus.Unresolved);

                    var currentBatch = new List<Tuple<string, ClassifiableMessageDetails>>();

                    using (var stream = session.Advanced.Stream(query.As<FailedMessage>()))
                    {
                        while (!abort && stream.MoveNext())
                        {
                            if (stream.Current.Document.FailureGroups.Count > 0)
                            {
                                continue;
                            }

                            currentBatch.Add(Tuple.Create(stream.Current.Document.Id, new ClassifiableMessageDetails(stream.Current.Document)));

                            if (currentBatch.Count == BatchSize)
                            {
                                ReclassifyBatch(currentBatch);
                                currentBatch.Clear();
                            }
                        }
                    }

                    if (currentBatch.Any())
                    {
                        ReclassifyBatch(currentBatch);
                    }

                    logger.Info("Reclassification of failures ended.");

                    if (settings == null)
                    {
                        settings = new ReclassifyErrorSettings();
                    }

                    settings.ReclassificationDone = true;
                    session.Store(settings);
                    session.SaveChanges();
                }

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

        void ReclassifyBatch(IEnumerable<Tuple<string, ClassifiableMessageDetails>> docs)
        {
            Parallel.ForEach(docs, doc =>
            {
                var failureGroups = GetClassificationGroups(doc.Item2).Select(RavenJObject.FromObject);

                try
                {
                    store.DatabaseCommands.Patch(doc.Item1,
                        new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "FailureGroups",
                                Value = new RavenJArray(failureGroups),
                            }
                        });
                    Interlocked.Increment(ref failedMessagesReclassified);
                }
                catch (ConcurrencyException)
                {
                    // Ignore concurrency exceptions
                }
            });
        }

        IEnumerable<FailedMessage.FailureGroup> GetClassificationGroups(ClassifiableMessageDetails details)
        {
            foreach (var classifier in classifiers)
            {
                var classification = classifier.ClassifyFailure(details);
                if (classification == null)
                {
                    continue;
                }

                var id = DeterministicGuid.MakeId(classifier.Name, classification).ToString();

                yield return new FailedMessage.FailureGroup
                {
                    Id = id,
                    Title = classification,
                    Type = classifier.Name
                };
            }
        }
    }
}