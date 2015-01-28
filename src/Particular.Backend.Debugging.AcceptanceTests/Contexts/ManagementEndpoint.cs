namespace Particular.Backend.Debugging.AcceptanceTests.Contexts
{
    using NServiceBus.AcceptanceTesting;

    public class ManagementEndpoint : EndpointConfigurationBuilder
    {
        public ManagementEndpoint()
        {
            EndpointSetup<ManagementEndpointSetup>();
        }
    }
}