﻿namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class ServiceControlEndpointTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_configure_endpoint()
        {
            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ServiceControlEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(transportSettings, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizePrimaryEndpoint(ec, transportSettings);
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
            public ServiceControlEndpoint() =>
                EndpointSetup<BasicEndpointSetup>(c =>
                {
                    c.UsePersistence<NonDurablePersistence>();
                });
        }
    }
}