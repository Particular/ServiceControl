namespace ServiceControl.AcceptanceTests.RavenDB.Shared;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBus.Management.Infrastructure.Settings;

static class WebApplicationBuilderExtensions
{
    public static void AddServiceControlTesting(this WebApplicationBuilder hostBuilder, Settings settings)
    {
        // Do not register additional test controllers or hosted services here. Instead, in the test that needs them, use (for example):
        // CustomizeHostBuilder = builder => builder.ConfigureServices((hostContext, services) => services.AddHostedService<SetupNotificationSettings>());
        hostBuilder.Logging.AddScenarioContextLogging();

        hostBuilder.WebHost.UseTestServer(options => options.BaseAddress = new Uri(settings.ServiceControl.RootUrl));

        // This facilitates receiving the test server anywhere where DI is available
        hostBuilder.Services.AddSingleton(provider => (TestServer)provider.GetRequiredService<IServer>());

        // For acceptance testing purposes we are adding more controllers to the host
        var addControllers = hostBuilder.Services.AddControllers();
        addControllers.AddApplicationPart(typeof(AcceptanceTest).Assembly);

        hostBuilder.Services.AddHttpClientDefaultsOverrides(settings);
    }
}