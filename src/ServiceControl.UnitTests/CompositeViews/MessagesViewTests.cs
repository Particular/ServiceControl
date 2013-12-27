namespace ServiceControl.UnitTests.CompositeViews
{
    using System.Linq;
    using Contracts.Operations;
    using MessageAuditing;
    using NUnit.Framework;
    using Raven.Client.Linq;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    public class MessagesViewTests
    {
        [Test]
        public void Filter_out_system_messages()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                documentStore.Initialize();

                var customIndex = new MessagesViewIndex();
                customIndex.Execute(documentStore);

                var transformer = new MessagesViewTransformer();

                transformer.Execute(documentStore);

                using (var session = documentStore.OpenSession())
                {
                    var processedMessage = new ProcessedMessage
                    {
                        Id = "1",
                    };

                    processedMessage.MessageMetadata["IsSystemMessage"] = new MessageMetadata("IsSystemMessage", true);
                    session.Store(processedMessage);
                    var processedMessage2 = new ProcessedMessage
                    {
                        Id = "2",
                    };

                    processedMessage2.MessageMetadata["IsSystemMessage"] = new MessageMetadata("IsSystemMessage", false);
                    session.Store(processedMessage2);
                    session.SaveChanges();

                    var results = session.Query<MessagesViewIndex.Result, MessagesViewIndex>()
                        .Customize(x => x.WaitForNonStaleResults())
                        .Where(x => !x.IsSystemMessage)
                       .OfType<ProcessedMessage>()
                        .ToList();
                    Assert.AreEqual(1, results.Count);
                    Assert.AreNotEqual("1", results.Single().Id);
                }

            }
        }

        [SetUp]
        public void SetUp()
        {
            
        }
    }
}