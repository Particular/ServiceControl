namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Monitoring;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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

            Assert.AreEqual(1, endpoints.Length);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, endpoints.First().Name);
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

            Assert.IsFalse(endpoint.Monitored, "Endpoint detected through error ingestion should not be monitored");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}