namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

static class WebApplicationBuilderExtensions
{
    public static void AddServiceControlMonitoringTesting(this WebApplicationBuilder hostBuilder, Settings settings)
    {
        hostBuilder.Logging.AddScenarioContextLogging();

        hostBuilder.WebHost.UseTestServer(options => options.BaseAddress = new Uri(settings.RootUrl));
        // This facilitates receiving the test server anywhere where DI is available
        hostBuilder.Services.AddKeyedSingleton(settings.ServiceName,
            (provider, _) => (TestServer)provider.GetRequiredService<IServer>());

        // // For acceptance testing purposes we are adding more controllers to the host
        var addControllers = hostBuilder.Services.AddControllers();
        addControllers.AddApplicationPart(typeof(AcceptanceTest).Assembly);
    }
}