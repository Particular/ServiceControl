namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.HealthMonitoring.Uptime;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_an_unmonitored_endpoint_is_marked_as_monitored : AcceptanceTest
    {
        enum State
        {
            WaitingForEndpointDetection,
            WaitingForHeartbeatFailure
        }

        static string EndpointName => Conventions.EndpointNamingConvention(typeof(MyEndpoint));

        [Test]
        public void It_is_shown_as_inactive_if_it_does_not_send_heartbeats()
        {
            var context = new MyContext();
            List<EndpointsView> endpoints = null;
            var state = State.WaitingForEndpointDetection;
            Define(context)
                .WithEndpoint<MyEndpoint>()
                .Done(c =>
                {
                    if (state == State.WaitingForEndpointDetection)
                    {
                        var found = TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName && !e.Monitored);
                        if (found)
                        {
                            var endpointId = endpoints.First(e => e.Name == EndpointName).Id;
                            Patch($"/api/endpoints/{endpointId}",new EndpointUpdateModel
                            {
                                MonitorHeartbeat = true
                            });
                            state = State.WaitingForHeartbeatFailure;
                            Console.WriteLine("Patch successful");
                        }
                        return false;
                    }
                    return state == State.WaitingForHeartbeatFailure && TryGetMany("/api/endpoints/", out endpoints, e => e.Name == EndpointName && e.MonitorHeartbeat && e.Monitored && !e.IsSendingHeartbeats);
                })
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsTrue(myEndpoint.Monitored);
            Assert.IsTrue(myEndpoint.MonitorHeartbeat);
            Assert.IsFalse(myEndpoint.IsSendingHeartbeats);
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

        public class MyMessage : IMessage
        {
        }
    }
}