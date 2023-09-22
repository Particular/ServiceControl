using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.Tests.RavenDb5;
using ServiceControl.Persistence.Tests.RavenDb5.ObjectExtensions;

[TestFixture]
class ErrorMessageDataStoreTests : PersistenceTestBase
{
    IDocumentStore documentStore;
    IErrorMessageDataStore store;
    FailedMessage processedMessage1, processedMessage2;

    [Test]
    public async Task GetAllMessages()
    {
        var result = await store.GetAllMessages(new PagingInfo(1, 50), new SortInfo("", ""), false);
        Assert.IsNotEmpty(result.Results);
    }

    [Test]
    [TestCase("", "asc", "a")]
    [TestCase("", "dsc", "b")]
    [TestCase("message_id", "asc", "a")]
    [TestCase("message_id", "dsc", "b")]
    [TestCase("critical_time", "asc", "a")]
    [TestCase("critical_time", "dsc", "b")]
    public async Task GetAllMessagesForEndpoint(string sort, string direction, string id)
    {
        var result = await store.GetAllMessagesForEndpoint(
            "RamonAndTomek",
            new PagingInfo(1, 1),
            new SortInfo(sort, direction),
            false
            );

        Assert.IsNotEmpty(result.Results);
        Assert.AreEqual(1, result.Results.Count);
        Assert.AreEqual(id, result.Results[0].Id);
    }

    [Test]
    public async Task ErrorGet()
    {
        var result = await store.ErrorGet(null, null, null, new PagingInfo(1, 50), new SortInfo("", ""));
        Assert.IsNotEmpty(result.Results);
    }

    [SetUp]
    public async Task GetStore()
    {
        await Task.Yield();
        await SetupDocumentStore();
        await GenerateAndSaveFailedMessage();

        CompleteDatabaseOperation();

        store = GetRequiredService<IErrorMessageDataStore>();
    }

    async Task GenerateAndSaveFailedMessage()
    {
        using (var session = documentStore.OpenAsyncSession())
        {
            processedMessage1 = FailedMessageBuilder.Build(m =>
            {
                m.Id = "1";
                m.UniqueMessageId = "a";
                m.ProcessingAttempts.First().MessageMetadata["ReceivingEndpoint"].CastTo<EndpointDetails>().Name = "RamonAndTomek";
                m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(5);
            });

            await session.StoreAsync(processedMessage1);

            processedMessage2 = FailedMessageBuilder.Build(m =>
            {
                m.Id = "2";
                m.UniqueMessageId = "b";
                m.ProcessingAttempts.First().MessageMetadata["ReceivingEndpoint"].CastTo<EndpointDetails>().Name = "RamonAndTomek";
                m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(15);
            });

            await session.StoreAsync(processedMessage2);

            await session.SaveChangesAsync();
        }
    }

    async Task SetupDocumentStore()
    {
        documentStore = GetRequiredService<IDocumentStore>();
        var customIndex = new FailedMessageViewIndex();
        await customIndex.ExecuteAsync(documentStore);
        //var transformer = new FailedMessageViewTransformer();
        //TODO: we need to bring this back
        //transformer.Execute(documentStore);
    }
}