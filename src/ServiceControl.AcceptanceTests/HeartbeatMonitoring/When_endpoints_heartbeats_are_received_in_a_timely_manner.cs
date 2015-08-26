namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
                    List<EndpointsView> endpoints;
                    if (!TryGetMany("/api/endpoints", out endpoints))
                    {
                        return false;
                    }

                    if (!endpoints.All(view => view.Name.Contains("Endpoint1") || view.Name.Contains("Endpoint2")))
                    {
                        return false;
                    }

                    return TryGet("/api/heartbeats/stats", out summary);
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(0, summary.Failing);
            Assert.AreEqual(2, summary.Active);
        }

        public class HeartbeatSummary
        {
            public int Active { get; set; }
            public int Failing { get; set; }
        }
    }
}