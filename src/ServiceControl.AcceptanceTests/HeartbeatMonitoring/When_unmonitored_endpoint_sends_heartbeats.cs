namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_unmonitored_endpoint_sends_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(WithoutHeartbeat));

        [Test]
        public async Task Should_be_marked_as_monitored()
        {
            List<EndpointsView> endpoints = null;

            await Define<MyContext>()
                .WithEndpoint<WithoutHeartbeat>(c => c.When(bus => bus.SendLocal(new MyMessage())))
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
                .WithEndpoint<WithHeartbeat>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats);
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

        public class WithoutHeartbeat : EndpointConfigurationBuilder
        {
            public WithoutHeartbeat()
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

        public class WithHeartbeat : EndpointConfigurationBuilder
        {
            public WithHeartbeat()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => { c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME); }).CustomEndpointName(EndpointName);
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}