namespace ServiceControl.Configuration;

using Microsoft.Extensions.Configuration;

public class SettingsReaderConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new SettingsReaderConfigurationProvider();
}