namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.HealthMonitoring.Uptime;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_unmonitored_endpoint_starts_sending_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(MyEndpoint));

        [Test]
        public void Should_be_marked_as_monitored()
        {
            var context = new MyContext();
            List<EndpointsView> endpoints = null;

            Define(context)
                .WithEndpoint<MyEndpoint>()
                .Done(c => TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName))
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsFalse(myEndpoint.Monitored);

            Define(context)
                .WithEndpoint<MyEndpointWithHeartbeat>()
                .Done(c => TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats))
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