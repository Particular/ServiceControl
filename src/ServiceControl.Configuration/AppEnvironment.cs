namespace ServiceControl.Configuration;

using System;

public static class AppEnvironment
{
    public static bool RunningInContainer { get; } = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
}