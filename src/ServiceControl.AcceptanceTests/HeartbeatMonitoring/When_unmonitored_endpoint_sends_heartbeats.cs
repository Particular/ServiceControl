﻿namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.EndpointControl;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_unmonitored_endpoint_sends_heartbeats : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(WithoutHeartbeat));

        [Test]
        public async Task Should_be_marked_as_monitored()
        {
            EndpointsView endpoint = null;

            await Define<MyContext>()
                .WithEndpoint<WithoutHeartbeat>(c => c.When(bus =>
                {
                    var options = new SendOptions();

                    options.DoNotEnforceBestPractices();
                    options.SetDestination(Settings.DEFAULT_SERVICE_NAME);

                    return bus.Send(new NewEndpointDetected
                    {
                        Endpoint = new ServiceControl.Contracts.Operations.EndpointDetails
                        {
                            HostId = Guid.NewGuid(),
                            Host = "myhost",
                            Name = EndpointName
                        },
                        DetectedAt = DateTime.UtcNow
                    }, options);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", e => e.Name == EndpointName);

                    endpoint = result.Item;

                    return result.HasResult;
                })
                .Run();

            Assert.IsFalse(endpoint.MonitorHeartbeat);
            Assert.IsFalse(endpoint.Monitored);

            await Define<MyContext>()
                .WithEndpoint<WithHeartbeat>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", e => e.Name == EndpointName && e.IsSendingHeartbeats);

                    endpoint = result.Item;

                    return result.HasResult;
                })
                .Run();

            Assert.IsTrue(endpoint.MonitorHeartbeat, "Should have heartbeat monitoring on");
            Assert.IsTrue(endpoint.Monitored, "Should be flagged as monitored");
            Assert.IsTrue(endpoint.IsSendingHeartbeats, "Should be emitting heartbeats");
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
        }

        public class WithHeartbeat : EndpointConfigurationBuilder
        {
            public WithHeartbeat()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => { c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME); }).CustomEndpointName(EndpointName);
            }
        }
    }
}