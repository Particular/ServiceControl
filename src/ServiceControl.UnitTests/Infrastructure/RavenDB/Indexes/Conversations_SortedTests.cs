using System;
using System.Linq;
using NUnit.Framework;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using ServiceBus.Management.Infrastructure.RavenDB.Indexes;
using ServiceBus.Management.MessageAuditing;

[TestFixture]
public class Conversations_SortedTests
{
    [Test]
    public void Simple()
    {
        using (var documentStore = GetInMemoryStore())
        {
            documentStore.Initialize();

            var defaultIndex = new RavenDocumentsByEntityName();
            defaultIndex.Execute(documentStore);

            var customIndex = new Conversations_Sorted();
            customIndex.Execute(documentStore);

            using (var session = documentStore.OpenSession())
            {
                session.Store(new Message
                {
                    Id = "1",
                    MessageType = "MessageType1",
                    TimeSent = DateTime.Now,
                    Status = MessageStatus.Successful,
                    ConversationId = "2",
                });
                session.SaveChanges();
                var results = session.Query<Conversations_Sorted.Result, Conversations_Sorted>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<Message>()
                    .ToList();
                Assert.AreEqual(1, results.Count);
            }

        }
    }

    static EmbeddableDocumentStore GetInMemoryStore()
    {
        return new EmbeddableDocumentStore
        {
            Configuration =
            {
                RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true, 
                RunInMemory = true
            }
        };
    }
}