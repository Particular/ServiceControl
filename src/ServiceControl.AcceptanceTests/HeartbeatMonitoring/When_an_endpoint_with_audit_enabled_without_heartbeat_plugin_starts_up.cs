namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_endpoint_with_audit_enabled_without_heartbeat_plugin_starts_up : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(StartingEndpoint));

        [Test]
        public async Task Should_not_be_monitored()
        {
            List<EndpointsView> endpoints = null;

            await Define<MyContext>()
                .WithEndpoint<StartingEndpoint>(c => c.When(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    
                    var result = await TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName);
                    endpoints = result;
                    return result;
                })
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsFalse(myEndpoint.Monitored);
        }

        public class MyContext : ScenarioContext
        {
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}