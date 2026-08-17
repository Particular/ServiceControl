namespace ServiceControl.Recoverability
{
    using System.Threading;
    using System.Threading.Tasks;
    using Connection;
    using ServiceBus.Management.Infrastructure.Settings;

    class RecoverabilityPlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly Settings settings;

        public RecoverabilityPlatformConnectionDetailsProvider(Settings settings) => this.settings = settings;

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection, CancellationToken cancellationToken = default)
        {
            connection.Add("ErrorQueue", settings.ErrorQueue);
            return Task.CompletedTask;
        }
    }
}