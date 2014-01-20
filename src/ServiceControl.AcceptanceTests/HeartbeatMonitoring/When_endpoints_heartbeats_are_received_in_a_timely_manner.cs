namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using Contexts;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;

    public class When_endpoints_heartbeats_are_received_in_a_timely_manner : AcceptanceTest
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
                        if (!TryGet("/api/heartbeats/stats", out summary, m=> m.ActiveEndpoints >= 2))
                        {
                            return false;
                        }

                        List<EndpointsView> endpoints;

                        return TryGetMany("/api/endpoints", out endpoints, e => e.Name.Contains("Endpoint1") );

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