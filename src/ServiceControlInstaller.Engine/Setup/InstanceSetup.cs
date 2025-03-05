namespace ServiceControlInstaller.Engine.Setup;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Instances;

static class InstanceSetup
{
    public static void Run(IServiceControlInstance instance) =>
        Run(instance.InstallPath,
            Constants.ServiceControlExe,
            instance.Name,
            instance.SkipQueueCreation);

    public static void Run(IServiceControlAuditInstance instance) =>
        Run(instance.InstallPath,
            Constants.ServiceControlAuditExe,
            instance.Name,
            instance.SkipQueueCreation);

    public static void Run(IMonitoringInstance instance) =>
        Run(instance.InstallPath,
            Constants.MonitoringExe,
            instance.Name,
            instance.SkipQueueCreation);

    static void Run(string installPath, string exeName, string instanceName, bool skipQueueCreation)
    {
        var args = $"--setup";

        if (skipQueueCreation)
        {
            args += " --skip-queue-creation";
        }

        // we will wait "forever" since that is the most safe action right not. We will leave it up to the setup code in the instances to make sure it won't run forever.
        // If/when provide better UI experience we might revisit this and potentially find a way for the installer to control the timeout.
        Run(installPath, exeName, instanceName, args, Timeout.Infinite);
    }

    internal static Process Run(string installPath, string exeName, string instanceName, string args, int timeout)
    {
        var processStartupInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            FileName = Path.Combine(installPath, exeName),
            Arguments = args,
            WorkingDirectory = installPath,
            RedirectStandardError = true
        };

        processStartupInfo.EnvironmentVariables.Add("INSTANCE_NAME", instanceName);

        var p = Process.Start(processStartupInfo) ?? throw new Exception($"Attempt to launch {exeName} failed.");

        p.WaitForExit(timeout);

        if (!p.HasExited || p.ExitCode == 0)
        {
            return p;
        }

        var error = p.StandardError.ReadToEnd();

        throw new Exception($"{exeName} returned a non-zero exit code: {p.ExitCode}. This typically indicates a configuration error. The error output was:\r\n {error}");
    }
}