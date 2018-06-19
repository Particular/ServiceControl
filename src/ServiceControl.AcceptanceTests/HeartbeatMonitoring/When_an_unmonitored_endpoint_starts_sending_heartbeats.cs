namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_unmonitored_endpoint_starts_sending_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(MyEndpoint));

        [Test]
        public async Task Should_be_marked_as_monitored()
        {
            List<EndpointsView> endpoints = null;

            await Define<MyContext>()
                .WithEndpoint<MyEndpoint>(c => c.When(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName);
                    endpoints = result;
                    return result;
                })
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsFalse(myEndpoint.Monitored);

            await Define<MyContext>()
                .WithEndpoint<MyEndpointWithHeartbeat>()
                .Done(async c =>
                {
                    var result =  await this.TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats);
                    endpoints = result;
                    return result;
                })
                .Run();

            myEndpoint = endpoints.SingleOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsTrue(myEndpoint.Monitored);
            Assert.IsTrue(myEndpoint.IsSendingHeartbeats);
        }

        public class MyContext : ScenarioContext
        {
        }

        public class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEndpointWithHeartbeat : EndpointConfigurationBuilder
        {
            public MyEndpointWithHeartbeat()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME);
                }).CustomEndpointName(EndpointName);
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}