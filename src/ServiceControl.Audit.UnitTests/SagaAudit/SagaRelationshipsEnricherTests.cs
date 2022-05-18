namespace ServiceControl.Audit.UnitTests.SagaAudit
{
    using System.Collections.Generic;
    using Audit.Auditing;
    using Audit.SagaAudit;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class SagaRelationshipsEnricherTests
    {
        [Test]
        public void New_overrides_Updated_state()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("New", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Updated_does_not_override_new()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("New", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Updated_does_not_override_completed()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Completed;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("Completed", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Completed_overrides_new()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Completed"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("Completed", sagaData[0].ChangeStatus);
        }

        [Test]
        public void New_does_not_override_completed()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Completed;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("Completed", sagaData[0].ChangeStatus);
        }

        [Test]
        public void It_can_parse_malformed_headers_of_three_sagas()
        {
            var enricher = new SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga2:9bc8ff00-9e10-41f6-8a56-a79a0060a762ConsoleApp1.MySaga2:9bc8ff00-9e10-41f6-8a56-a79a0060a762;ConsoleApp1.MySaga3:6f46e0e9-5476-4141-a1fd-a79a0060a782ConsoleApp1.MySaga2:9bc8ff00-9e10-41f6-8a56-a79a0060a762ConsoleApp1.MySaga2:9bc8ff00-9e10-41f6-8a56-a79a0060a762;ConsoleApp1.MySaga3:6f46e0e9-5476-4141-a1fd-a79a0060a782;ConsoleApp1.MySaga:c0ed4546-ddce-42d7-9ce2-a79a0060a782",
                ["ServiceControl.SagaStateChange"] = "c0ed4546-ddce-42d7-9ce2-a79a0060a782:Updated;6f46e0e9-5476-4141-a1fd-a79a0060a782:New;9bc8ff00-9e10-41f6-8a56-a79a0060a762:Completed"
            };

            var metadata = new Dictionary<string, object>();
            var commandsToEmit = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();

            enricher.Enrich(new AuditEnricherContext(headers, commandsToEmit, transportOperations, metadata));

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(3, sagaData.Count);
        }
    }
}