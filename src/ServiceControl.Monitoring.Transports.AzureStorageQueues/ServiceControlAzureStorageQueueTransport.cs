namespace ServiceControl.Transports.AzureStorageQueues
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ServiceControlAzureStorageQueueTransport : AzureStorageQueueTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            extensions.SanitizeQueueNamesWith(QueueNameSanitizer.Sanitize);

            return base.Initialize(settings, connectionString);
        }
    }
}
