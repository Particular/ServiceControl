namespace ServiceControl.UnitTests.SagaAudit
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class SagaRelationshipsEnricherTests
    {
        [Test]
        public void It_can_process_a_message_that_invoked_the_same_saga_instance_twice()
        {
            var enricher = new SagaAuditing.SagaRelationshipsEnricher();

            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = "ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8;ConsoleApp1.MySaga:51b5ad68-8ac4-46ee-a39c-a79900ca4ea8",
                ["ServiceControl.SagaStateChange"] = "51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:New;51b5ad68-8ac4-46ee-a39c-a79900ca4ea8:Updated"
            };

            var metadata = new Dictionary<string, object>();

            enricher.Enrich(headers, metadata);
        }
    }
}