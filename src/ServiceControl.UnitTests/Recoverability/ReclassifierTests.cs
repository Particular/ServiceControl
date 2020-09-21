namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using Raven.Client.Documents;
    using ServiceControl.Contracts.Operations;
    using MessageFailures.Api;
    using ServiceControl.Recoverability;
    using NUnit.Framework;
    using Raven.TestDriver;

    [TestFixture]
    public class ReclassifierTests : RavenTestDriver
    {
        IDocumentStore Store { get; set; }
        Reclassifier Reclassifier { get; set; }

        [SetUp]
        public void Setup()
        {
            Store = GetDocumentStore();
            Reclassifier = new Reclassifier(null);
        }

        [Test]
        public async Task Should_reclassify_old_documents()
        {
            IEnumerable<IFailureClassifier> classifiers = new IFailureClassifier[] {new FakeClassifier()};

            var documentId = await CreateMessage().ConfigureAwait(true);

            new FailedMessageViewIndex().Execute(Store);
            WaitForIndexing(Store);

            var reclassifyFailedMessages = await Reclassifier.ReclassifyFailedMessages(Store, true, classifiers).ConfigureAwait(true);

            Assert.AreEqual(1, reclassifyFailedMessages);
            using (var asyncDocumentSession = Store.OpenAsyncSession())
            {
                var failedMessage = await asyncDocumentSession.LoadAsync<FailedMessage>(documentId).ConfigureAwait(true);

                Assert.IsNotNull(failedMessage.FailureGroups);
                Assert.IsNotEmpty(failedMessage.FailureGroups);
                Assert.AreEqual("Foo", failedMessage.FailureGroups[0].Title);
                Assert.AreEqual("Bar", failedMessage.FailureGroups[0].Type);
            }
        }

        private async Task<string> CreateMessage()
        {
            var messageId = Guid.NewGuid().ToString();
            var metadata = new Dictionary<string, object>();
            metadata.Add("MessageId", messageId);
            metadata.Add("MessageType", "Type");
            metadata.Add("TimeSent", DateTime.Now);
            metadata.Add("ReceivingEndpoint",
                new EndpointDetails() {HostId = Guid.NewGuid(), Host = "Mike", Name = "of the South"});

            var processingAttempt = new FailedMessage.ProcessingAttempt()
            {
                MessageId = messageId,
                FailureDetails = new FailureDetails()
                    {AddressOfFailingEndpoint = "address", TimeOfFailure = DateTime.Now},
                AttemptedAt = DateTime.Now, MessageMetadata = metadata
            };
            var documentId = Guid.NewGuid().ToString();
            var message = new FailedMessage()
            {
                Id = documentId, Status = FailedMessageStatus.Unresolved,
                FailureGroups = new List<FailedMessage.FailureGroup>()
                {
                    new FailedMessage.FailureGroup()
                        {Id = Guid.NewGuid().ToString(), Title = "Lord", Type = "It does not matter"}
                },
                ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>() {processingAttempt},
                UniqueMessageId = Guid.NewGuid().ToString(),
            };

            using (var asyncDocumentSession = Store.OpenAsyncSession())
            {
                await asyncDocumentSession.StoreAsync(message).ConfigureAwait(true);
                await asyncDocumentSession.SaveChangesAsync().ConfigureAwait(true);
            }

            return documentId;
        }

        class FakeClassifier : IFailureClassifier
        {
            public string Name => "Bar";
            public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
            {
                return "Foo";
            }
        }
    }
}