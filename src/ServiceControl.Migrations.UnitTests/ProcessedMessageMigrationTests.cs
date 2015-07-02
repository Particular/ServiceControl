namespace ServiceControl.Migrations.UnitTests
{
    using System;
    using NUnit.Framework;
    using ObjectApproval;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Abstractions.Data;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Raven.Json.Linq;

    [TestFixture]
    class ProcessedMessageMigrationTests
    {
        EmbeddableDocumentStore store;

        [SetUp]
        public void RunMigration()
        {
            store = InMemoryStoreBuilder.GetInMemoryStore();
            new RavenDocumentsByEntityName().Execute(store);
            var metadata = new RavenJObject
                {
                    {"Raven-Entity-Name", new RavenJValue("ProcessedMessages")}
                };
            store.DatabaseCommands.Put("ProcessedMessages/e2c66e1c-3167-2d25-e681-3044635fc2f4", Etag.Empty, RavenJObject.Parse(OldFailedMessageDocument), metadata);

            store.WaitForIndexing();
            var expiryThreshold = TimeSpan.FromDays(50 * 365);
            var migration = new ProcessedMessageMigration(expiryThreshold);
            migration.Setup(store);
            migration.Up().Wait();
        }

        [TearDown]
        public void Clean()
        {
            store.Dispose();
        }

        [Test]
        public void It_deletes_the_migrated_document()
        {
            using (var session = store.OpenSession())
            {
                var failedMessage = session.Load<ProcessedMessage>("ProcessedMessages/e2c66e1c-3167-2d25-e681-3044635fc2f4");
                Assert.IsNull(failedMessage);
            }
        }

        [Test]
        public void It_creates_a_message_snapshot_for_debugging()
        {
            using (var session = store.OpenSession())
            {
                var snapshot = session.Load<MessageSnapshotDocument>("AuditMessageSnapshots/e2c66e1c-3167-2d25-e681-3044635fc2f4");
                Assert.IsNotNull(snapshot);
                ObjectApprover.VerifyWithJson(snapshot);
            }
        }

        const string OldFailedMessageDocument = @"{
  ""UniqueMessageId"": ""e2c66e1c-3167-2d25-e681-3044635fc2f4"",
  ""MessageMetadata"": {
    ""MessageId"": ""5626e4b6-a2e9-40a2-86ae-a445008dc208"",
    ""MessageIntent"": 1,
    ""HeadersForSearching"": ""5626e4b6-a2e9-40a2-86ae-a445008dc208 5626e4b6-a2e9-40a2-86ae-a445008dc208 Sender.Default fa2c673600d1e72ad99147355265111d Send 4.6.5 2015-02-20 07:36:07:493470 Z SIMON-MAC text/xml ServiceBus.Management.AcceptanceTests.When_a_message_has_been_successfully_processed+MyMessage, ServiceBus.Management.AcceptanceTests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null 5626e4b6-a2e9-40a2-86ae-a445008dc208\\0  3fcb74c3-4405-4649-8f64-a445008dc211 SIMON-MAC Receiver.Default fa2c673600d1e72ad99147355265111d SIMON-MAC 2015-02-20 07:36:10:959990 Z false 2015-02-20 07:36:11:467349 Z Sender.Default@SIMON-MAC"",
    ""TimeSent"": ""2015-02-20T07:36:07.4934700Z"",
    ""CriticalTime"": ""00:00:03.9738790"",
    ""ProcessingTime"": ""00:00:00.5073590"",
    ""DeliveryTime"": ""00:00:03.4665200"",
    ""ContentType"": ""text/xml"",
    ""BodyUrl"": ""/messages/5626e4b6-a2e9-40a2-86ae-a445008dc208/body"",
    ""Body"": ""<?xml version=\""1.0\"" ?>\r\n<Messages xmlns:xsi=\""http://www.w3.org/2001/XMLSchema-instance\"" xmlns:xsd=\""http://www.w3.org/2001/XMLSchema\"" xmlns=\""http://tempuri.net/ServiceBus.Management.AcceptanceTests\"">\n<MyMessage>\n</MyMessage>\n</Messages>\r\n"",
    ""ContentLength"": 237,
    ""SearchableMessageType"": ""ServiceBus Management AcceptanceTests When_a_message_has_been_successfully_processed MyMessage"",
    ""IsSystemMessage"": false,
    ""MessageType"": ""ServiceBus.Management.AcceptanceTests.When_a_message_has_been_successfully_processed+MyMessage"",
    ""IsRetried"": false,
    ""SendingEndpoint"": {
      ""$type"": ""ServiceControl.Contracts.Operations.EndpointDetails, ServiceControl.InternalContracts"",
      ""Name"": ""Sender.Default"",
      ""HostId"": ""fa2c6736-00d1-e72a-d991-47355265111d"",
      ""Host"": ""SIMON-MAC""
    },
    ""ReceivingEndpoint"": {
      ""$type"": ""ServiceControl.Contracts.Operations.EndpointDetails, ServiceControl.InternalContracts"",
      ""Name"": ""Receiver.Default"",
      ""HostId"": ""fa2c6736-00d1-e72a-d991-47355265111d"",
      ""Host"": ""SIMON-MAC""
    },
    ""ConversationId"": ""3fcb74c3-4405-4649-8f64-a445008dc211""
  },
  ""Headers"": {
    ""NServiceBus.MessageId"": ""5626e4b6-a2e9-40a2-86ae-a445008dc208"",
    ""NServiceBus.CorrelationId"": ""5626e4b6-a2e9-40a2-86ae-a445008dc208"",
    ""NServiceBus.OriginatingEndpoint"": ""Sender.Default"",
    ""$.diagnostics.originating.hostid"": ""fa2c673600d1e72ad99147355265111d"",
    ""NServiceBus.MessageIntent"": ""Send"",
    ""NServiceBus.Version"": ""4.6.5"",
    ""NServiceBus.TimeSent"": ""2015-02-20 07:36:07:493470 Z"",
    ""NServiceBus.OriginatingMachine"": ""SIMON-MAC"",
    ""NServiceBus.ContentType"": ""text/xml"",
    ""NServiceBus.EnclosedMessageTypes"": ""ServiceBus.Management.AcceptanceTests.When_a_message_has_been_successfully_processed+MyMessage, ServiceBus.Management.AcceptanceTests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"",
    ""CorrId"": ""5626e4b6-a2e9-40a2-86ae-a445008dc208\\0"",
    ""WinIdName"": """",
    ""NServiceBus.ConversationId"": ""3fcb74c3-4405-4649-8f64-a445008dc211"",
    ""NServiceBus.ProcessingMachine"": ""SIMON-MAC"",
    ""NServiceBus.ProcessingEndpoint"": ""Receiver.Default"",
    ""$.diagnostics.hostid"": ""fa2c673600d1e72ad99147355265111d"",
    ""$.diagnostics.hostdisplayname"": ""SIMON-MAC"",
    ""NServiceBus.ProcessingStarted"": ""2015-02-20 07:36:10:959990 Z"",
    ""$.diagnostics.license.expired"": ""false"",
    ""NServiceBus.ProcessingEnded"": ""2015-02-20 07:36:11:467349 Z"",
    ""NServiceBus.OriginatingAddress"": ""Sender.Default@SIMON-MAC""
  },
  ""ProcessedAt"": ""2015-02-20T07:36:11.4673490Z""
}";
    }
}