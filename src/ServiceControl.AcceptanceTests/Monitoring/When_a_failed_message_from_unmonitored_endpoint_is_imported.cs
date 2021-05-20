namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using TestSupport;
    using TestSupport.EndpointTemplates;

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