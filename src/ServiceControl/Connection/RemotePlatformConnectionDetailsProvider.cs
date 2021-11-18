namespace ServiceControl.Connection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class RemotePlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly Settings settings;
        readonly Func<HttpClient> httpClientFactory;

        public RemotePlatformConnectionDetailsProvider(Settings settings, Func<HttpClient> httpClientFactory)
        {
            this.settings = settings;
            this.httpClientFactory = httpClientFactory;
        }

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection) =>
            Task.WhenAll(
                settings.RemoteInstances
                    .Select(remote => UpdateFromRemote(remote, connection))
            );

        async Task UpdateFromRemote(RemoteInstanceSetting remote, PlatformConnectionDetails connection)
        {
            using (var client = httpClientFactory())
            {
                try
                {
                    var result = await client.GetStringAsync($"{remote.ApiUri}/connection")
                        .ConfigureAwait(false);
                    var dictionary = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result);
                    if (dictionary == null)
                    {
                        Log.Warn($"Unexpected response from ${remote.ApiUri}/connection: ${result}");
                        return;
                    }

                    foreach (var kvp in dictionary)
                    {
                        connection.Add(kvp.Key, kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Unable to get connection details from ${remote.ApiUri}/connection";
                    connection.Status.Exceptions.Add(message);
                    connection.Status.IsSuccess = false;

                    Log.Error(message, ex);
                }
            }
        }

        static readonly ILog Log = LogManager.GetLogger<RemotePlatformConnectionDetailsProvider>();
    }
}