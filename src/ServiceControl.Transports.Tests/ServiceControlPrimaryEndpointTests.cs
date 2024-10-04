﻿namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;

    partial class ServiceControlPrimaryEndpointTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_configure_endpoint()
        {
            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ServiceControlPrimaryEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(transportSettings, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlPrimaryEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizePrimaryEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
        }

        [TestCase(15)]
        [TestCase(null)]
        public async Task Should_set_max_concurrency(int? setConcurrency)
        {
            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ServiceControlPrimaryEndpoint));
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName
            };

            await configuration.TransportCustomization.ProvisionQueues(new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = endpointName
            }, []);

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlPrimaryEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizePrimaryEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted, Is.True);
            Assert.That(transportSettings.MaxConcurrency == (setConcurrency ?? GetTransportDefaultConcurrency()));
        }

        private partial int GetTransportDefaultConcurrency();

        public class Context : ScenarioContext;

        public class ServiceControlPrimaryEndpoint : EndpointConfigurationBuilder
        {
            public ServiceControlPrimaryEndpoint() =>
                EndpointSetup<BasicEndpointSetup>(c =>
                {
                    c.UsePersistence<NonDurablePersistence>();
                });
        }
    }
}