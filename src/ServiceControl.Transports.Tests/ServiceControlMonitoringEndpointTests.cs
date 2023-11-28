﻿namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Transports;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class ServiceControlMonitoringEndpointTests : TransportTestFixture
    {
        [Test]
        public async Task Should_configure_monitoring_endpoint()
        {
            string endpointName = Conventions.EndpointNamingConvention(typeof(ServiceControlEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(WindowsIdentity.GetCurrent().Name, transportSettings, new List<string>());

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeMonitoringEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(ctx.EndpointsStarted);
        }

        public class Context : ScenarioContext
        {
        }

        public class ServiceControlEndpoint : EndpointConfigurationBuilder
        {
            public ServiceControlEndpoint()
            {
                EndpointSetup<BasicEndpointSetup>(c =>
                {
                    c.UsePersistence<NonDurablePersistence>();
                });
            }
        }
    }
}