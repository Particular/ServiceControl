namespace ServiceBus.Management.Infrastructure.Settings;

using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;

class PrimaryOptionsPostConfiguration : IPostConfigureOptions<PrimaryOptions> // TODO: Register
{
    public void PostConfigure(string name, PrimaryOptions options)
    {
        var suffix = string.Empty;
        if (!string.IsNullOrEmpty(options.VirtualDirectory))
        {
            suffix = $"{options.VirtualDirectory}/";
        }

        options.RootUrl = $"http://{options.Hostname}:{options.Port}/{suffix}";

        if (AppEnvironment.RunningInContainer)
        {
            options.Hostname = "*";
            options.Port = 33333;
        }

        options.ConnectionString ??= System.Configuration.ConfigurationManager.ConnectionStrings["NServiceBus/Transport"]?.ConnectionString;
        options.InstanceId = InstanceIdGenerator.FromApiUrl(options.ApiUrl);

        options.RemoteInstanceSettings = string.IsNullOrEmpty(options.RemoteInstances)
            ? []
            : ParseRemoteInstances(options.RemoteInstances);
    }

    internal static RemoteInstanceSetting[] ParseRemoteInstances(string value) => JsonSerializer.Deserialize<RemoteInstanceSetting[]>(value, SerializerOptions.Default) ?? [];
}