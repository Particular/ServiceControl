namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;

    public class ManagementEndpoint : EndpointConfigurationBuilder
    {
        public ManagementEndpoint()
        {
            EndpointSetup<ManagementEndpointSetup>(c=>Configure.Features.Disable<Audit>());
        }
    }
}