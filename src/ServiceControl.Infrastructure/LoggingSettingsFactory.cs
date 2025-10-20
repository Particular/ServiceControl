namespace ServiceControl.Infrastructure;

using Microsoft.Extensions.Configuration;

public static class LoggingSettingsFactory
{
    public static LoggingSettings Create(IConfiguration configuration)
    {
        var opt = configuration.Get<LoggingOptions>();
        var dst = new LoggingSettings();
        LoggingOptionsToSettings.Map(opt, dst);
        return dst;
    }
}