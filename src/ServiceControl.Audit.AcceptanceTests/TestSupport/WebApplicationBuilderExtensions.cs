namespace ServiceControl.Audit.AcceptanceTests.TestSupport;

using System;
using Auditing;
using Infrastructure.Settings;
using Infrastructure.WebApi;
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

        // By default, ASP.NET Core uses the entry point assembly to discover controllers. When running
        // inside a test runner the runner exe becomes the entry point which obviously has no controllers in it ;)
        // so we are explicitly registering all necessary application parts.
        var addControllers = hostBuilder.Services.AddControllers();
        addControllers.AddApplicationPart(typeof(WebApiHostBuilderExtensions).Assembly);
        addControllers.AddApplicationPart(typeof(FailedAuditsController).Assembly);
    }
}