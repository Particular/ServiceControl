﻿namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using Contracts.EndpointControl;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Monitoring;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_an_unmonitored_endpoint_is_marked_as_monitored : AcceptanceTest
    {
        static string EndpointName => Conventions.EndpointNamingConvention(typeof(MyEndpoint));

        [Test]
        public async Task It_is_shown_as_inactive_if_it_does_not_send_heartbeats()
        {
            List<EndpointsView> endpoints = null;
            var state = State.WaitingForEndpointDetection;

            await Define<MyContext>()
                .WithEndpoint<MyEndpoint>(c => c.When(bus =>
                {
                    var options = new SendOptions();

                    options.DoNotEnforceBestPractices();
                    options.SetDestination(ServiceBus.Management.Infrastructure.Settings.Settings.DEFAULT_SERVICE_NAME);

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
                    if (state == State.WaitingForEndpointDetection)
                    {
                        var intermediateResult = await this.TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && !e.Monitored);
                        endpoints = intermediateResult;
                        if (intermediateResult)
                        {
                            var endpointId = endpoints.First(e => e.Name == EndpointName).Id;
                            await this.Patch($"/api/endpoints/{endpointId}", new EndpointUpdateModel
                            {
                                MonitorHeartbeat = true
                            });
                            state = State.WaitingForHeartbeatFailure;
                            Console.WriteLine("Patch successful");
                        }

                        return false;
                    }

                    var result = await this.TryGetMany<EndpointsView>("/api/endpoints/", e => e.Name == EndpointName && e.MonitorHeartbeat && e.Monitored && !e.IsSendingHeartbeats);
                    endpoints = result;
                    return state == State.WaitingForHeartbeatFailure && result;
                })
                .Run();

            var myEndpoint = endpoints.FirstOrDefault(e => e.Name == EndpointName);
            Assert.NotNull(myEndpoint);
            Assert.IsTrue(myEndpoint.Monitored);
            Assert.IsTrue(myEndpoint.MonitorHeartbeat);
            Assert.IsFalse(myEndpoint.IsSendingHeartbeats);
        }

        enum State
        {
            WaitingForEndpointDetection,
            WaitingForHeartbeatFailure
        }

        public class MyContext : ScenarioContext
        {
        }

        public class MyEndpoint : EndpointConfigurationBuilder
        {
            public MyEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}