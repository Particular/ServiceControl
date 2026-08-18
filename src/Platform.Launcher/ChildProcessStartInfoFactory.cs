namespace ServiceControl.Launcher;

using System.Diagnostics;

static class ChildProcessStartInfoFactory
{
    public static ProcessStartInfo Create(ChildLaunch child)
    {
        ArgumentNullException.ThrowIfNull(child);

        var startInfo = new ProcessStartInfo(child.Descriptor.ExecutablePath)
        {
            WorkingDirectory = child.Descriptor.WorkingDirectory,
            UseShellExecute = false
        };

        foreach (var argument in child.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentOverride in child.EnvironmentOverrides)
        {
            startInfo.Environment[environmentOverride.Key] = environmentOverride.Value;
        }

        return startInfo;
    }
}
