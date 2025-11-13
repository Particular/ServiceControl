namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

class ServiceBusOptionsPostConfiguration(ILogger<Settings> logger) : IPostConfigureOptions<ServiceBusOptions> // TODO: Register
{
    public void PostConfigure(string name, ServiceBusOptions options)
    {
        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for audit log queue to import, default name will be used");
            options.ErrorLogQueue = Subscope(options.ErrorLogQueue);
        }
    }

    static string Subscope(string address)
    {
        var atIndex = address.IndexOf("@", StringComparison.InvariantCulture);

        if (atIndex <= -1)
        {
            return $"{address}.log";
        }

        var queue = address.Substring(0, atIndex);
        var machine = address.Substring(atIndex + 1);
        return $"{queue}.log@{machine}";
    }
}