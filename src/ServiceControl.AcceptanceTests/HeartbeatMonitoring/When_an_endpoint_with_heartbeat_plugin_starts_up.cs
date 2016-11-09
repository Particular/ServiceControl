namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_endpoint_with_heartbeat_plugin_starts_up : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(StartingEndpoint));

        [Test]
        public void Should_be_monitored_and_active()
        {
            var context = new MyContext();
            List<EndpointsView> endpoints = null;

            Define(context)
                .WithEndpoint<StartingEndpoint>()
                .Done(c => TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName && e.Monitored && e.MonitorHeartbeat && e.IsSendingHeartbeats))
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsTrue(myEndpoint.Monitored);
            Assert.IsTrue(myEndpoint.IsSendingHeartbeats);
        }

        public class MyContext : ScenarioContext
        {
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EnableFeature<Audit>();
                }).IncludeAssembly(Assembly.LoadFrom("ServiceControl.Plugin.Nsb5.Heartbeat.dll"));
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

        public class MyMessage : IMessage
        {
        }
    }
}