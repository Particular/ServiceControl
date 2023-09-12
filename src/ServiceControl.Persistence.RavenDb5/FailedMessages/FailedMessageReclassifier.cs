namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json.Linq;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    class FailedMessageReclassifier : IReclassifyFailedMessages
    {
        readonly IDocumentStore store;
        readonly IEnumerable<IFailureClassifier> classifiers;

        public FailedMessageReclassifier(IDocumentStore store, IHostApplicationLifetime applicationLifetime, IEnumerable<IFailureClassifier> classifiers)
        {
            this.store = store;
            this.classifiers = classifiers;

            applicationLifetime?.ApplicationStopping.Register(() => { abort = true; });
        }

        public async Task<int> ReclassifyFailedMessages(bool force)
        {
            logger.Info("Reclassification of failures started.");

            var failedMessagesReclassified = 0;
            var currentBatch = new List<Tuple<string, ClassifiableMessageDetails>>();

            using (var session = store.OpenAsyncSession())
            {
                ReclassifyErrorSettings settings = null;

                if (!force)
                {
                    settings = await session.LoadAsync<ReclassifyErrorSettings>(ReclassifyErrorSettings.IdentifierCase);

                    if (settings != null && settings.ReclassificationDone)
                    {
                        logger.Info("Skipping reclassification of failures as classification has already been done.");
                        return 0;
                    }
                }

                var query = session.Query<FailedMessage, FailedMessageViewIndex>()
                    .Where(f => f.Status == FailedMessageStatus.Unresolved);

                var totalMessagesReclassified = 0;

                await using (var stream = await session.Advanced.StreamAsync(query.OfType<FailedMessage>()))
                {
                    while (!abort && await stream.MoveNextAsync())
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

                settings ??= new ReclassifyErrorSettings();

                settings.ReclassificationDone = true;
                await session.StoreAsync(settings);
                await session.SaveChangesAsync();

                return failedMessagesReclassified;
            }
        }

        int ReclassifyBatch(IDocumentStore store, IEnumerable<Tuple<string, ClassifiableMessageDetails>> docs, IEnumerable<IFailureClassifier> classifiers)
        {
            var failedMessagesReclassified = 0;

            Parallel.ForEach(docs, doc =>
            {
                var failureGroups = GetClassificationGroups(doc.Item2, classifiers).Select(JObject.FromObject);

                try
                {
                    store.DatabaseCommands.Patch(doc.Item1,
                        new[]
                        {
                            new PatchRequest
                            {
                                Type = PatchCommandType.Set,
                                Name = "FailureGroups",
                                Value = new JArray(failureGroups)
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

        readonly ILog logger = LogManager.GetLogger<FailedMessageReclassifier>();
        const int BatchSize = 1000;
        bool abort;
    }
}
