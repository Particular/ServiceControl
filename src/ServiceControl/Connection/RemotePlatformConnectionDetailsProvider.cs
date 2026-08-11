namespace ServiceControl.Connection
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class RemotePlatformConnectionDetailsProvider(Settings settings, IHttpClientFactory clientFactory, IHttpContextAccessor httpContextAccessor, ILogger<RemotePlatformConnectionDetailsProvider> logger)
        : IProvidePlatformConnectionDetails
    {
        public Task ProvideConnectionDetails(PlatformConnectionDetails connection, CancellationToken cancellationToken = default)
        {
            var authorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

            return Task.WhenAll(
                settings.RemoteInstances
                    .Select(remote => UpdateFromRemote(remote, connection, authorizationHeader, cancellationToken))
            );
        }

        async Task UpdateFromRemote(RemoteInstanceSetting remote, PlatformConnectionDetails connection, string authorizationHeader, CancellationToken cancellationToken)
        {
            var client = clientFactory.CreateClient(remote.InstanceId);
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/connection");
                var hasAuth = !string.IsNullOrEmpty(authorizationHeader);

                if (hasAuth)
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
                }

                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    connection.Add(property.Name, property.Value);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var remoteConnectionUri = $"{remote.BaseAddress.TrimEnd('/')}/connection";

                connection.Errors.Add($"Unable to get connection details from ServiceControl Audit instance at {remoteConnectionUri}.");
                logger.LogError(ex, "Unable to get connection details from ServiceControl Audit instance at {RemoteInstanceUrl}", remoteConnectionUri);
            }
        }
    }
}