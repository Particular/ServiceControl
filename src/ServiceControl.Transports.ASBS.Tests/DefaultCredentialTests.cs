namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Transports;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class DefaultCredentialTests : FullEndpointTestFixture
    {
        [Test]
        public async Task Should_authenticate_using_default_credentials_when_FQNS_is_used()
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(DefaultCredentialEndpoint));

            var connectionStringProperties = ServiceBusConnectionStringProperties.Parse(configuration.ConnectionString);

            var fullyQualifiedNamespace = connectionStringProperties.FullyQualifiedNamespace;

            var transportSettings = new TransportSettings
            {
                ConnectionString = fullyQualifiedNamespace,
                MaxConcurrency = 1,
                EndpointName = endpointName
            };

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<DefaultCredentialEndpoint>(c => c.CustomConfig(ec =>
                {
                    configuration.TransportCustomization.CustomizePrimaryEndpoint(ec, transportSettings);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.That(ctx.EndpointsStarted.Task.IsCompletedSuccessfully, Is.True);
        }

        public class Context : ScenarioContext
        {
        }

        public class DefaultCredentialEndpoint : EndpointConfigurationBuilder
        {
            public DefaultCredentialEndpoint() =>
                EndpointSetup<BasicEndpointSetup>(c =>
                {
                    c.UsePersistence<NonDurablePersistence>();
                });
        }
    }
}