namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
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
                EndpointSetup<DefaultServerWithoutAudit>();
            }
        }

        public class Endpoint2 : EndpointConfigurationBuilder
        {
            public Endpoint2()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
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
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Endpoint1>()
                .WithEndpoint<Endpoint2>()
                .Done(c =>
                    {
                        summary = Get<HeartbeatSummary>("/api/heartbeats/stats");

                        if (summary == null)
                        {
                            Thread.Sleep(2000);
                            return false;
                        }

                        if (summary.ActiveEndpoints < 2)
                        {
                            Thread.Sleep(2000);
                        }

                        return summary.ActiveEndpoints == 2;
                    })
                .Run(TimeSpan.FromMinutes(2));


            Assert.AreEqual(0, summary.NumberOfFailingEndpoints);
        }

        public class HeartbeatSummary
        {
            public int ActiveEndpoints { get; set; }
            public int NumberOfFailingEndpoints { get; set; }
        }
    }
}