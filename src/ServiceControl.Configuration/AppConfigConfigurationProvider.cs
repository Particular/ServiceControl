#nullable enable

namespace ServiceControl.Configuration;

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

public class AppConfigConfigurationProvider : ConfigurationProvider
{
    public AppConfigConfigurationProvider(Dictionary<string, string[]> mappings)
    {
        foreach (var (msConfigurationExtensionKey, appConfigKeys) in mappings)
        {
            foreach (var appConfigKey in appConfigKeys)
            {
                var appConfigValue = System.Configuration.ConfigurationManager.AppSettings[appConfigKey];

                if (appConfigValue is not null)
                {
                    Data[msConfigurationExtensionKey] = appConfigValue;
                }
            }
        }
    }
}
