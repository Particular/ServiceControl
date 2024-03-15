namespace ServiceControl.Audit.AcceptanceTests.TestSupport;

using System;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

static class WebApplicationBuilderExtensions
{
    public static void AddServiceControlAuditTesting(this WebApplicationBuilder hostBuilder, Settings settings)
    {
        // Do not register additional test controllers or hosted services here. Instead, in the test that needs them, use (for example):
        // CustomizeHostBuilder = builder => builder.ConfigureServices((hostContext, services) => services.AddHostedService<SetupNotificationSettings>());
        hostBuilder.Logging.AddScenarioContextLogging();

        hostBuilder.WebHost.UseTestServer(options => options.BaseAddress = new Uri(settings.RootUrl));
        // This facilitates receiving the test server anywhere where DI is available
        hostBuilder.Services.AddSingleton(provider => (TestServer)provider.GetRequiredService<IServer>());

        // For acceptance testing purposes we are adding more controllers to the host
        var addControllers = hostBuilder.Services.AddControllers();
        addControllers.AddApplicationPart(typeof(AcceptanceTest).Assembly);
    }
}