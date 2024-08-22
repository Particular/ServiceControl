namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_a_failed_message_from_unmonitored_endpoint_is_imported : AcceptanceTest
    {
        [Test]
        public async Task It_is_detected()
        {
            EndpointsView[] endpoints = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var result = await this.TryGet<EndpointsView[]>("/api/endpoints/");
                    endpoints = result;
                    return endpoints.Length > 0;
                })
                .Run();

            Assert.That(endpoints.Length, Is.EqualTo(1));
            Assert.That(endpoints.First().Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint));
        }

        [Test]
        public async Task It_is_persisted()
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(Receiver));
            KnownEndpoint endpoint = default;

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var knownEndpoints = await this.TryGetSingle<KnownEndpoint>("/api/test/knownendpoints/query",
                        k => k.EndpointDetails.Name == endpointName);

                    endpoint = knownEndpoints.Item;
                    return knownEndpoints.HasResult;
                })
                .Run();

            Assert.That(endpoint.Monitored, Is.False, "Endpoint detected through error ingestion should not be monitored");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext
        {
            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}