namespace ServiceControlInstaller.Engine.Setup;

using System;
using System.Diagnostics;
using System.IO;
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

        var p = Process.Start(processStartupInfo);
        if (p != null)
        {
            var error = p.StandardError.ReadToEnd();
            p.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
            if (!p.HasExited)
            {
                p.Kill();
                throw new TimeoutException($"Timed out waiting for {exeName} to perform setup. This usually indicates a configuration error.");
            }

            if (p.ExitCode != 0)
            {
                throw new Exception($"{exeName} threw an error when performing setup. This typically indicates a configuration error. The error output from {exeName} was:\r\n {error}");
            }
        }
        else
        {
            throw new Exception($"Attempt to launch {exeName} failed.");
        }
    }
}