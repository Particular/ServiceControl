namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Connection;
    using Microsoft.Extensions.Options;
    using ServiceBus.Management.Infrastructure.Settings;

    class RecoverabilityPlatformConnectionDetailsProvider(IOptions<ServiceBusOptions> options) : IProvidePlatformConnectionDetails
    {
        public Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            connection.Add("ErrorQueue", options.Value.ErrorQueue);
            return Task.CompletedTask;
        }
    }
}