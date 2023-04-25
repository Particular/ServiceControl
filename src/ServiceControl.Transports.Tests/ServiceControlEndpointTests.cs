namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class ServiceControlEndpointTests : TransportTestFixture
    {
        [Test]
        public async Task Should_configure_endpoint()
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ServiceControlEndpoint));
            var username = WindowsIdentity.GetCurrent().Name;

            await ProvisionQueues(username, endpointName, "InstanceName.Errors", new List<string>());

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<ServiceControlEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizeServiceControlEndpoint(ec, transportSettings);
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
                EndpointSetup<BasicEndpointSetup>(configuration =>
                {
                    configuration.DisableFeature<Audit>();
                    configuration.DisableFeature<AutoSubscribe>();
                    configuration.DisableFeature<TimeoutManager>();
                    configuration.DisableFeature<Outbox>();
                    configuration.DisableFeature<Sagas>();
                    configuration.SendFailedMessagesTo("InstanceName.Errors");

                    configuration.UsePersistence<InMemoryPersistence>();
                });
            }
        }
    }
}