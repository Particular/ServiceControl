namespace ServiceControl.Infrastructure;

using Microsoft.Extensions.Logging;

public record LoggingOptions
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; }
    public string LoggingProviders { get; set; }
    public string SeqAddress { get; set; }
}

public class LoggingSettings // TODO: Register
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public string LogPath { get; set; }
}
