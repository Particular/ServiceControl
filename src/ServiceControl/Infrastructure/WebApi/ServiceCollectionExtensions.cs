namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using Settings;

static class ServiceCollectionExtensions
{
    public static void AddRemoteInstancesHttpClients(this IServiceCollection services, Settings settings)
    {
        // TODO move this configuration to an extension method
        foreach (var remoteInstance in settings.RemoteInstances)
        {
            remoteInstance.InstanceId = InstanceIdGenerator.FromApiUrl(remoteInstance.ApiUri);
            var remoteClientBuilder = services.AddHttpClient(remoteInstance.InstanceId, client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // Application settings might contain remote URLs with /api. We strip that away to be a real base address.
                client.BaseAddress = new Uri(remoteInstance.ApiUri.Replace("/api", string.Empty));
            });

            remoteClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        }
    }
}