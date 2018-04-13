namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_unmonitored_endpoint_starts_sending_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(MyEndpoint));

        [Test]
        public async Task Should_be_marked_as_monitored()
        {
            var context = new MyContext();
            List<EndpointsView> endpoints = null;

            await Define(context)
                .WithEndpoint<MyEndpoint>()
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

            await Define(context)
                .WithEndpoint<MyEndpointWithHeartbeat>()
                .Done(async c =>
                {
                    var result =  await TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats);
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

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly IBus bus;

                public SendMessage(IBus bus)
                {
                    this.bus = bus;
                }

                public void Start()
                {
                    bus.SendLocal(new MyMessage());
                }

                public void Stop()
                {
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                }
            }
        }

        public class MyEndpointWithHeartbeat : EndpointConfigurationBuilder
        {
            public MyEndpointWithHeartbeat()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EndpointName(EndpointName);
                }).IncludeAssembly(Assembly.LoadFrom("ServiceControl.Plugin.Nsb5.Heartbeat.dll"));
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}