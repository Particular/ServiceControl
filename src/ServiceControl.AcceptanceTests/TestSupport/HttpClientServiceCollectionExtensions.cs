namespace ServiceControl.AcceptanceTests.RavenDB.Shared;

using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;

static class HttpClientServiceCollectionExtensions
{
    public static void OverrideHttpClientDefaults(this IServiceCollection services, Settings settings)
    {
        services.AddKeyedSingleton("Forwarding", (provider, _) => provider.GetRequiredService<TestServer>());
        services.AddSingleton(p => new HttpMessageInvoker(p.GetRequiredKeyedService<TestServer>("Forwarding").CreateHandler()));

        foreach (var remoteInstance in settings.RemoteInstances)
        {
            services.AddKeyedSingleton(remoteInstance.InstanceId, (provider, _) => provider.GetRequiredService<TestServer>());
            var remoteInstanceHttpClientBuilder = services.AddHttpClient(remoteInstance.InstanceId);
            remoteInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredKeyedService<TestServer>(remoteInstance.InstanceId).CreateHandler());
        }
    }
}