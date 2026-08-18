namespace ServiceControl.Launcher;

static class LauncherShutdownTimeout
{
    public const string EnvironmentVariable = "SERVICECONTROL_LAUNCHER_SHUTDOWN_TIMEOUT";
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(20);

    public static TimeSpan Parse(string? value)
    {
        if (value is null)
        {
            return Default;
        }

        var trimmed = value.Trim();
        TimeSpan timeout;
        if (trimmed.EndsWith('s') &&
            double.TryParse(trimmed[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            timeout = TimeSpan.FromSeconds(seconds);
        }
        else if (!TimeSpan.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out timeout))
        {
            throw InvalidTimeout();
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw InvalidTimeout();
        }

        return timeout;
    }

    static LauncherConfigurationException InvalidTimeout() => new(
        $"{EnvironmentVariable} must be a positive duration, for example '20s' or '00:00:20'.");
}