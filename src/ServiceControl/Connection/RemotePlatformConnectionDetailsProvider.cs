namespace ServiceControl.Connection
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class RemotePlatformConnectionDetailsProvider(Settings settings, IHttpClientFactory clientFactory)
        : IProvidePlatformConnectionDetails
    {
        public Task ProvideConnectionDetails(PlatformConnectionDetails connection) =>
            Task.WhenAll(
                settings.RemoteInstances
                    .Select(remote => UpdateFromRemote(remote, connection))
            );

        async Task UpdateFromRemote(RemoteInstanceSetting remote, PlatformConnectionDetails connection)
        {
            var remoteConnectionUri = $"{remote.ApiUri.TrimEnd('/')}/connection";

            var client = clientFactory.CreateClient(remote.InstanceId);
            try
            {
                await using var stream = await client.GetStreamAsync("/connection");
                var document = await JsonDocument.ParseAsync(stream);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    connection.Add(property.Name, property.Value);
                }
            }
            catch (Exception ex)
            {
                var message = $"Unable to get connection details from ServiceControl Audit instance at {remoteConnectionUri}.";

                connection.Errors.Add(message);

                Log.Error(message, ex);
            }
        }

        static readonly ILog Log = LogManager.GetLogger<RemotePlatformConnectionDetailsProvider>();
    }
}