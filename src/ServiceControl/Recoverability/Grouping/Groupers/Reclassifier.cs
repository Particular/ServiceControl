namespace ServiceControl.Recoverability
{
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Reclassifier
    {
        const int BatchSize = 1000;
        private bool abort;

        ILog logger = LogManager.GetLogger<Reclassifier>();

        internal Reclassifier(ShutdownNotifier notifier)
        {
            notifier?.Register(() => { abort = true; });
        }

        internal int ReclassifyFailedMessages(IDocumentStore store, bool force, IEnumerable<IFailureClassifier> classifiers)
        {
            logger.Info("Reclassification of failures started.");

            int failedMessagesReclassified = 0;
            var currentBatch = new List<Tuple<string, ClassifiableMessageDetails>>();

            using (var session = store.OpenSession())
            {
                ReclassifyErrorSettings settings = null;

                if (!force)
                {
                    settings = session.Load<ReclassifyErrorSettings>(ReclassifyErrorSettings.IdentifierCase);

                    if (settings != null && settings.ReclassificationDone)
                    {
                        logger.Info("Skipping reclassification of failures as classification has already been done.");
                        return 0;
                    }
                }

                var query = session.Query<FailedMessage, FailedMessageViewIndex>()
                            .Where(f => f.Status == FailedMessageStatus.Unresolved);

                var totalMessagesReclassified = 0;

                using (var stream = session.Advanced.Stream(query.As<FailedMessage>()))
                {
                    while (!abort && stream.MoveNext())
                    {
                        currentBatch.Add(Tuple.Create(stream.Current.Document.Id, new ClassifiableMessageDetails(stream.Current.Document)));

                        if (currentBatch.Count == BatchSize)
                        {
                            failedMessagesReclassified += ReclassifyBatch(store, currentBatch, classifiers);
                            currentBatch.Clear();

                            totalMessagesReclassified += BatchSize;
                            logger.Info($"Reclassification of batch of {BatchSize} failed messages completed. Total messages reclassified: {totalMessagesReclassified}");
                        }
                    }
                }

                if (currentBatch.Any())
                {
                    ReclassifyBatch(store, currentBatch, classifiers);
                }

                logger.Info($"Reclassification of failures ended. Reclassified {failedMessagesReclassified} messages");

                if (settings == null)
                {
                    settings = new ReclassifyErrorSettings();
                }

                settings.ReclassificationDone = true;
                session.Store(settings);
                session.SaveChanges();

                return failedMessagesReclassified;
            }
        }

        int ReclassifyBatch(IDocumentStore store, IEnumerable<Tuple<string, ClassifiableMessageDetails>> docs, IEnumerable<IFailureClassifier> classifiers)
        {
            int failedMessagesReclassified = 0;

            Parallel.ForEach(docs, doc =>
            {
                var failureGroups = GetClassificationGroups(doc.Item2, classifiers).Select(RavenJObject.FromObject);

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

            return failedMessagesReclassified;
        }

        IEnumerable<FailedMessage.FailureGroup> GetClassificationGroups(ClassifiableMessageDetails details, IEnumerable<IFailureClassifier> classifiers)
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