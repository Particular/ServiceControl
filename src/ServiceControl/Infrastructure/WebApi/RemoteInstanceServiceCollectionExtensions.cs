namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using Settings;
using Yarp.ReverseProxy.Forwarder;

static class RemoteInstanceServiceCollectionExtensions
{
    public const string RemoteForwardingHttpClientName = "remote-forwarding";

    public static void AddHttpForwarding(this IServiceCollection services)
    {
        services.AddHttpForwarder();
        var httpClientBuilder = services.AddHttpClient(RemoteForwardingHttpClientName);
        httpClientBuilder.UseSocketsHttpHandler((handler, _) =>
        {
            handler.UseProxy = false;
            handler.AllowAutoRedirect = false;
            handler.AutomaticDecompression = DecompressionMethods.None;
            handler.UseCookies = false;
            handler.ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current);
            handler.ConnectTimeout = TimeSpan.FromSeconds(15);
        });
    }

    public static void AddRemoteInstancesHttpClients(this IServiceCollection services, Settings settings)
    {
        foreach (var remoteInstance in settings.RemoteInstances)
        {
            var remoteClientBuilder = services.AddHttpClient(remoteInstance.InstanceId, client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // Application settings might contain remote URLs with /api. We strip that away to be a real base address.
                client.BaseAddress = new Uri(remoteInstance.ApiUri.Replace("/api", string.Empty));
            });

            remoteClientBuilder.UseSocketsHttpHandler((handler, _) =>
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            });
        }
    }
}