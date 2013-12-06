using System;
using System.Linq;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.RavenDB.Indexes;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageAuditing;

[TestFixture]
public class Messages_SearchTests
{
    [Test]
    public void Simple()
    {
        using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
        {
            documentStore.Initialize();
            
            var customIndex = new Messages_Search();
            customIndex.Execute(documentStore);

            using (var session = documentStore.OpenSession())
            {
                var timeSent = DateTime.Now;
                session.Store(new AuditMessage
                    {
                        Id = "id",
                        MessageType = "MessageType",
                        TimeSent = timeSent,
                        ConversationId = "ConversationId",
                    });
                session.SaveChanges();

                var results = session.Query<Messages_Search.Result, Messages_Search>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<AuditMessage>()
                    .ToList();
                Assert.AreEqual(1, results.Count);
                var message = results.First();
                Assert.AreEqual("id", message.Id);
                Assert.AreEqual("MessageType", message.MessageType);
                Assert.AreEqual(timeSent, message.TimeSent);
                Assert.AreEqual("ConversationId", message.ConversationId);
            }

        }
    }
}