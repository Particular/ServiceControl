namespace ServiceControl.AcceptanceTests.RavenDB.Shared;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;
using static Infrastructure.WebApi.RemoteInstanceServiceCollectionExtensions;

static class HttpClientServiceCollectionExtensions
{
    public static void OverrideHttpClientDefaults(this IServiceCollection services, Settings settings)
    {
        var testInstanceHttpClientBuilder = services.AddHttpClient(settings.ServiceName);
        testInstanceHttpClientBuilder.ConfigureHttpClient(httpClient =>
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        testInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<TestServer>().CreateHandler());

        var forwardingHttpClientBuilder = services.AddHttpClient(RemoteForwardingHttpClientName);
        forwardingHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<TestServer>().CreateHandler());

        foreach (var remoteInstance in settings.RemoteInstances)
        {
            var remoteInstanceHttpClientBuilder = services.AddHttpClient(remoteInstance.InstanceId);
            remoteInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<TestServer>().CreateHandler());
        }
    }
}