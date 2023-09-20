using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.PersistenceTests;
using ServiceControl.SagaAudit;

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
        await CompleteDatabaseOperation();

        store = GetRequiredService<IErrorMessageDataStore>();
    }

    async Task GenerateAndSaveFailedMessage()
    {
        using (var session = documentStore.OpenAsyncSession())
        {
            processedMessage1 = new FailedMessage
            {
                Id = "1",
                UniqueMessageId = "a",
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            Headers =
                            {
                                ["Tomek"]="Wizard",
                                ["Ramon"]="Cool",
                            },
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata =
                            {
                                ["TimeSent"]="2023-09-20T12:00:00",
                                ["MessageId"]="x",
                                ["MessageType"]="MyType",
                                ["SendingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="RamonAndTomek"},
                                ["ReceivingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="RamonAndTomek"},
                                ["ConversationId"]="abc",
                                ["MessageIntent"]="Send",
                                ["BodyUrl"]="https://particular.net",
                                ["ContentLength"]=11111,
                                ["InvokedSagas"]=new[]{new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, RamonAndTomek"}},
                                ["OriginatesFromSaga"]=new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, RamonAndTomek"},
                                ["CriticalTime"]=TimeSpan.FromSeconds(5),
                                ["ProcessingTime"]=TimeSpan.FromSeconds(5),
                                ["DeliveryTime"]=TimeSpan.FromSeconds(5),
                                ["IsSystemMessage"]=false,
                            },
                            FailureDetails = new FailureDetails()
                        }
                    }
            };
            await session.StoreAsync(processedMessage1);

            processedMessage2 = new FailedMessage
            {
                Id = "2",
                UniqueMessageId = "b",
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            Headers =
                            {
                                ["Tomek"]="Wizard",
                                ["Ramon"]="Cool",
                            },
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata =
                            {
                                ["TimeSent"]="2023-09-20T12:00:05",
                                ["MessageId"]="y",
                                ["MessageType"]="MyType",
                                ["SendingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="RamonAndTomek"},
                                ["ReceivingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="RamonAndTomek"},
                                ["ConversationId"]="abc",
                                ["MessageIntent"]="Send",
                                ["BodyUrl"]="https://particular.net",
                                ["ContentLength"]=22222,
                                ["InvokedSagas"]=new[]{new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, RamonAndTomek"}},
                                ["OriginatesFromSaga"]=new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, RamonAndTomek"},
                                ["CriticalTime"]=TimeSpan.FromSeconds(15),
                                ["ProcessingTime"]=TimeSpan.FromSeconds(15),
                                ["DeliveryTime"]=TimeSpan.FromSeconds(15),
                                ["IsSystemMessage"]=false,
                            },
                            FailureDetails = new FailureDetails()
                        }
                    }
            };
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