namespace ServiceControl.Connection
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
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

        public async Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            using (var client = httpClientFactory())
            {
                // TODO: Do this in parallel
                foreach (var remoteInstance in settings.RemoteInstances)
                {
                    // TODO: Handle failures properly
                    var result = await client.GetStringAsync($"{remoteInstance.ApiUri}/connection")
                        .ConfigureAwait(false);
                    var dictionary = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result);
                    if (dictionary == null)
                    {
                        // TODO: Handle this condition properly
                        continue;
                    }
                    foreach (var kvp in dictionary)
                    {
                        connection.Add(kvp.Key, kvp.Value);
                    }
                }
            }
        }
    }
}