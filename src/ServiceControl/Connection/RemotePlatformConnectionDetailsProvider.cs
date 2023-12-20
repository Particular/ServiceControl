﻿namespace ServiceControl.Connection
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
        readonly IHttpClientFactory httpClientFactory;

        public RemotePlatformConnectionDetailsProvider(Settings settings, IHttpClientFactory httpClientFactory)
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
            var remoteConnectionUri = $"{remote.ApiUri.TrimEnd('/')}/connection";

            var client = httpClientFactory.CreateClient(remote.InstanceId);
            try
            {
                var result = await client.GetStringAsync("/connection");
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result);
                if (dictionary == null)
                {
                    Log.Warn($"Unexpected response from {remoteConnectionUri}: {result}");
                    return;
                }

                foreach (var kvp in dictionary)
                {
                    connection.Add(kvp.Key, kvp.Value);
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