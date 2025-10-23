namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure;

static class HostBuilderExtensions
{
    public static void SetupApplicationConfiguration(this IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddLegacyAppSettings()
            .AddEnvironmentVariables();

        hostBuilder.Services.AddOptions<Settings>()
            .Services
            .ConfigureOptions<ConfigureSettings>();

        hostBuilder.Services.AddOptions<LoggingOptions>()
            .Bind(hostBuilder.Configuration.GetSection(PrimaryOptions.SectionName));

        hostBuilder.Services.AddOptions<PrimaryOptions>()
            .Bind(hostBuilder.Configuration.GetSection(PrimaryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .Services
                .AddSingleton<IValidateOptions<PrimaryOptions>, SettingsValidation>()
                .AddSingleton<IPostConfigureOptions<PrimaryOptions>, PrimaryOptionsPostConfiguration>();

        hostBuilder.Services.AddOptions<ServiceBusOptions>()
            .Bind(hostBuilder.Configuration.GetSection(ServiceBusOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .Services
                .AddSingleton<IValidateOptions<ServiceBusOptions>, ServiceBusValidation>()
                .AddSingleton<IPostConfigureOptions<ServiceBusOptions>, ServiceBusOptionsPostConfiguration>();
    }
}