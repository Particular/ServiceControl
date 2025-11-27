using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using ConfigurationManager = System.Configuration.ConfigurationManager;

public class LegacyAppSettingsConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ConfigurationManager.AppSettings.AllKeys)
        {
            if (key is null)
            {
                continue;
            }

            var normalizedKey = key.Replace('/', ':');
            data[normalizedKey] = ConfigurationManager.AppSettings[key];
        }

        Data = data;
    }
}