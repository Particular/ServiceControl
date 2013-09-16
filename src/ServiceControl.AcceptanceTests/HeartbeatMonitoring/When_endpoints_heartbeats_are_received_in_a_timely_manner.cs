namespace ServiceControl.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Threading;
    using Contexts;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    public class When_endpoints_heartbeats_are_received_in_a_timely_manner: AcceptanceTest
    {
        public class Endpoint1 : EndpointConfigurationBuilder
        {
            public Endpoint1()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class Endpoint2 : EndpointConfigurationBuilder
        {
            public Endpoint2()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyContext : ScenarioContext
        {
        }

        [Test]
        public void Should_be_reflected_as_active_endpoints_in_the_heartbeat_summary()
        {
            var context = new MyContext();

            HeartbeatSummary summary = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>()
                .WithEndpoint<Endpoint1>()
                .WithEndpoint<Endpoint2>()
                .Done(c =>
                    {
                        summary = Get<HeartbeatSummary>("/api/heartbeats/stats");

                        if (summary == null)
                            return false;

                        if(summary.ActiveEndpoints < 2)
                            Thread.Sleep(1000);
                        return summary.ActiveEndpoints == 2;
                    })
                .Run(TimeSpan.FromSeconds(20));


            Assert.AreEqual(0, summary.NumberOfFailingEndpoints);
        }

        public class HeartbeatSummary
        {
            public int ActiveEndpoints { get; set; }
            public int NumberOfFailingEndpoints { get; set; }
        }
    }
}