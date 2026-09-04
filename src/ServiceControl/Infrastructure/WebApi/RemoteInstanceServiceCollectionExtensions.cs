namespace ServiceControl.Infrastructure.WebApi;

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence;
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

    /// <summary>
    /// How much longer than the query time limit a remote gets to answer: its own query is allowed that limit
    /// (the audit instance has the same setting), and its answer, a 504 included, still has to travel back.
    /// </summary>
    public static readonly TimeSpan ResponseMargin = TimeSpan.FromSeconds(30);

    public static void AddRemoteInstancesHttpClients(this IServiceCollection services, Settings settings)
    {
        foreach (var remoteInstance in settings.RemoteInstances)
        {
            var remoteClientBuilder = services.AddHttpClient(remoteInstance.InstanceId, (serviceProvider, client) =>
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // Application settings might contain remote URLs with /api. We strip that away to be a real base address.
                client.BaseAddress = new Uri(remoteInstance.BaseAddress);
                client.Timeout = serviceProvider.GetRequiredService<PersistenceSettings>().QueryTimeout + ResponseMargin;
            });

            remoteClientBuilder.UseSocketsHttpHandler((handler, _) =>
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            });
        }
    }
}