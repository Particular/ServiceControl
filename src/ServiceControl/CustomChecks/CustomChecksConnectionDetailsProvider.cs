namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Connection;
    using NServiceBus;
    using NServiceBus.Settings;

    class CustomChecksPlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly string instanceMainQueue;

        public CustomChecksPlatformConnectionDetailsProvider(ReadOnlySettings endpointSettings)
            => instanceMainQueue = endpointSettings.LocalAddress();

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            connection.Add(
                "CustomChecks",
                new
                {
                    CustomChecksQueue = instanceMainQueue
                });
            return Task.CompletedTask;
        }
    }
}