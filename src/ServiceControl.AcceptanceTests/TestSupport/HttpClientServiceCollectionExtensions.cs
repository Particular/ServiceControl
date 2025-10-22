namespace ServiceControl.AcceptanceTests.RavenDB.Shared;

using System;
using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;

static class HttpClientServiceCollectionExtensions
{
    public static void AddHttpClientDefaultsOverrides(this IServiceCollection services, Settings settings)
    {
        services.AddKeyedSingleton<Func<HttpMessageHandler>>("Forwarding", (provider, _) => () => provider.GetRequiredService<TestServer>().CreateHandler());
        services.AddSingleton(p => new HttpMessageInvoker(p.GetRequiredKeyedService<Func<HttpMessageHandler>>("Forwarding")()));

        foreach (var remoteInstance in settings.ServiceControl.RemoteInstanceSettings)
        {
            services.AddKeyedSingleton<Func<HttpMessageHandler>>(remoteInstance.InstanceId, (provider, _) => () => provider.GetRequiredService<TestServer>().CreateHandler());
            var remoteInstanceHttpClientBuilder = services.AddHttpClient(remoteInstance.InstanceId);
            remoteInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredKeyedService<Func<HttpMessageHandler>>(remoteInstance.InstanceId)());
        }
    }
}