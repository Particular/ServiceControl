namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
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
        public void Should_not_be_monitored()
        {
            var context = new MyContext();
            List<EndpointsView> endpoints = null;

            Define(context)
                .WithEndpoint<StartingEndpoint>()
                .Done(c => TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName))
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