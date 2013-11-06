using System.Linq;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.RavenDB.Indexes;
using ServiceBus.Management.MessageAuditing;

[TestFixture]
public class Messages_IdsTests
{
    [Test]
    public void Simple()
    {
        using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
        {
            documentStore.Initialize();


            var customIndex = new Messages_Ids();
            customIndex.Execute(documentStore);

            using (var session = documentStore.OpenSession())
            {
                session.Store(new Message
                    {
                        Id = "id",
                        ReceivingEndpoint = new EndpointDetails
                            {
                                Name = "EndpointName"
                            },
                    });
                session.SaveChanges();

                var results = session.Query<Messages_Ids.Result, Messages_Ids>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<Message>()
                    .ToList();
                Assert.AreEqual(1, results.Count);
                var message = results.First();
                Assert.AreEqual("id", message.Id);
                Assert.AreEqual("EndpointName", message.ReceivingEndpoint.Name);
            }

        }
    }
}