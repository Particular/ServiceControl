namespace ServiceControl.AcceptanceTests.RavenDB.Shared;

using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;

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

        services.AddSingleton(p => new HttpMessageInvoker(p.GetRequiredService<TestServer>().CreateHandler()));

        foreach (var remoteInstance in settings.RemoteInstances)
        {
            var remoteInstanceHttpClientBuilder = services.AddHttpClient(remoteInstance.InstanceId);
            remoteInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<TestServer>().CreateHandler());
        }
    }
}