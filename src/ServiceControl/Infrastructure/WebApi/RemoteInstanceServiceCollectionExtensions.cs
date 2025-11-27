namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using Yarp.ReverseProxy.Forwarder;

static class RemoteInstanceServiceCollectionExtensions
{
    public static void AddHttpForwarding(this IServiceCollection services)
    {
        services.AddHttpForwarder();
        // Always use HttpMessageInvoker rather than HttpClient, HttpClient buffers responses by default.
        // Buffering breaks streaming scenarios and increases memory usage and latency.
        // https://microsoft.github.io/reverse-proxy/articles/direct-forwarding.html#the-http-client
        services.AddSingleton(new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
            ConnectTimeout = TimeSpan.FromSeconds(15),
        }));
    }

    public static void AddRemoteInstancesHttpClients(this IServiceCollection services, Settings settings)
    {
        foreach (var remoteInstance in settings.ServiceControl.RemoteInstanceSettings)
        {
            var remoteClientBuilder = services.AddHttpClient(remoteInstance.InstanceId, client =>
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // Application settings might contain remote URLs with /api. We strip that away to be a real base address.
                client.BaseAddress = new Uri(remoteInstance.BaseAddress);
            });

            remoteClientBuilder.UseSocketsHttpHandler((handler, _) =>
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            });
        }
    }
}