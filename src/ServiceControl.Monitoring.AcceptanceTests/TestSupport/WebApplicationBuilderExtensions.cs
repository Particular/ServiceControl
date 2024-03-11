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
        hostBuilder.Services.AddKeyedSingleton(settings.EndpointName,
            (provider, _) => (TestServer)provider.GetRequiredService<IServer>());

        // By default ASP.NET Core uses entry point assembly to discover controllers from. When running
        // inside a test runner the runner exe becomes the entry point which obviously has no controllers in it ;)
        // so we are explicitly registering all necessary application parts.
        var addControllers = hostBuilder.Services.AddControllers();
        addControllers.AddApplicationPart(typeof(WebApplicationBuilderExtensions).Assembly);
    }
}