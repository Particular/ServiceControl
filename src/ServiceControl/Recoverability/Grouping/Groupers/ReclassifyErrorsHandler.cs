namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Raven.Client.Linq;
    using Raven.Json.Linq;
    using ServiceControl.Contracts.Operations;
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

        public ReclassifyErrorsHandler(IBus bus, IDocumentStore store, IEnumerable<IFailureClassifier> classifiers)
        {
            this.bus = bus;
            this.store = store;
            this.classifiers = classifiers;
        }

        public void Handle(ReclassifyErrors message)
        {
            using (var session = store.OpenSession())
            {
                var query = session.Query<FailedMessage, FailedMessageViewIndex>()
                    .Where(f => f.Status == FailedMessageStatus.Unresolved);

                var currentBatch = new List<Tuple<string, FailureDetails>>();

                using (var stream = session.Advanced.Stream(query.As<FailedMessage>()))
                {
                    while (stream.MoveNext())
                    {
                        if (stream.Current.Document.FailureGroups.Count > 0)
                        {
                            continue;
                        }

                        currentBatch.Add(Tuple.Create(stream.Current.Document.Id, stream.Current.Document.ProcessingAttempts.Last().FailureDetails));

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
            }

            if (failedMessagesReclassified > 0)
            {
                bus.Publish(new ReclassificationOfErrorMessageComplete
                {
                    NumberofMessageReclassified = failedMessagesReclassified
                });
            }
        }

        void ReclassifyBatch(IEnumerable<Tuple<string, FailureDetails>> docs)
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

        IEnumerable<FailedMessage.FailureGroup> GetClassificationGroups(FailureDetails details)
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