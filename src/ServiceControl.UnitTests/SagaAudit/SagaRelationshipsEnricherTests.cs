namespace ServiceControl.UnitTests.SagaAudit
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class SagaRelationshipsEnricherTests
    {
        [Test]
        public void New_overrides_Updated_state()
        {
            var enricher = new SagaAuditing.SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New"
            };

            var metadata = new Dictionary<string, object>();

            enricher.Enrich(headers, metadata);

            var sagaData = (List<SagaInfo>) metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("New", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Updated_does_not_verride_new()
        {
            var enricher = new SagaAuditing.SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated"
            };

            var metadata = new Dictionary<string, object>();

            enricher.Enrich(headers, metadata);

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("New", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Updated_does_not_verride_completed()
        {
            var enricher = new SagaAuditing.SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Completed;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated"
            };

            var metadata = new Dictionary<string, object>();

            enricher.Enrich(headers, metadata);

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("Completed", sagaData[0].ChangeStatus);
        }

        [Test]
        public void Completed_overrides_new()
        {
            var enricher = new SagaAuditing.SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Completed"
            };

            var metadata = new Dictionary<string, object>();

            enricher.Enrich(headers, metadata);

            var sagaData = (List<SagaInfo>)metadata["InvokedSagas"];

            Assert.AreEqual(1, sagaData.Count);
            Assert.AreEqual("Completed", sagaData[0].ChangeStatus);
        }
    }
}