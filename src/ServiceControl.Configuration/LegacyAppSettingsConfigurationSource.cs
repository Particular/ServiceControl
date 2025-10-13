using Microsoft.Extensions.Configuration;

public class LegacyAppSettingsConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new LegacyAppSettingsConfigurationProvider();
}