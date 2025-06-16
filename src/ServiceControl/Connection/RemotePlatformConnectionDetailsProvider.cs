namespace ServiceControl.Connection
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class RemotePlatformConnectionDetailsProvider(Settings settings, IHttpClientFactory clientFactory, ILogger<RemotePlatformConnectionDetailsProvider> logger)
        : IProvidePlatformConnectionDetails
    {
        public Task ProvideConnectionDetails(PlatformConnectionDetails connection) =>
            Task.WhenAll(
                settings.RemoteInstances
                    .Select(remote => UpdateFromRemote(remote, connection))
            );

        async Task UpdateFromRemote(RemoteInstanceSetting remote, PlatformConnectionDetails connection)
        {
            var client = clientFactory.CreateClient(remote.InstanceId);
            try
            {
                await using var stream = await client.GetStreamAsync("/api/connection");
                var document = await JsonDocument.ParseAsync(stream);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    connection.Add(property.Name, property.Value);
                }
            }
            catch (Exception ex)
            {
                var remoteConnectionUri = $"{remote.BaseAddress.TrimEnd('/')}/connection";
                var message = $"Unable to get connection details from ServiceControl Audit instance at {remoteConnectionUri}.";

                connection.Errors.Add(message);

                logger.LogError(ex, "Unable to get connection details from ServiceControl Audit instance at {remoteConnectionUri}", remoteConnectionUri);
            }
        }
    }
}