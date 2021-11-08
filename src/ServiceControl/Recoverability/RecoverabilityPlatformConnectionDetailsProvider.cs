namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Connection;
    using ServiceBus.Management.Infrastructure.Settings;

    class RecoverabilityPlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly Settings settings;

        public RecoverabilityPlatformConnectionDetailsProvider(Settings settings) => this.settings = settings;

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            connection.Add("ErrorQueue", settings.ErrorQueue);
            return Task.CompletedTask;
        }
    }
}