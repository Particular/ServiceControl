using Microsoft.Extensions.Configuration;

public static class LegacyAppSettingsExtensions
{
    public static IConfigurationBuilder AddLegacyAppSettings(this IConfigurationBuilder builder)
    {
        builder.Add(new LegacyAppSettingsConfigurationSource());
        return builder;
    }
}