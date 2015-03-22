namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using NServiceBus;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    public class JSonServer : DefaultServer
    {
        public override void SetSerializer(Configure configure)
        {
            Configure.Serialization.Json();
        }
    }
}