namespace ServiceControlInstaller.Engine.Setup
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;

    internal static class ServiceControlSetup
    {
        public static void RunInSetupMode(IServiceControlInstance instance, string overrideUserName = null)
        {
            var accountName = overrideUserName ?? instance.ServiceAccount;
            var userAccount =  UserAccount.ParseAccountName(accountName);

            string args = $"--setup --serviceName={instance.Name}";

            if (!userAccount.IsLocalSystem())
            {
                args += $" --userName=\"{userAccount.QualifiedName}\"";
            }

            var processStartupInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = Path.Combine(instance.InstallPath, "ServiceControl.exe"),
                Arguments = args,
                WorkingDirectory = instance.InstallPath,
#if DEBUG
                RedirectStandardOutput = true,
#endif
                RedirectStandardError = true
            };

            using (var p = Process.Start(processStartupInfo))
            {
                if (p != null)
                {
#if DEBUG
                    p.OutputDataReceived += (sender, eventArgs) =>
                    {
                        Debug.WriteLine(eventArgs.Data);
                    };

                    p.BeginOutputReadLine();
#endif
                    var error = p.StandardError.ReadToEnd();
                    p.WaitForExit((int) TimeSpan.FromMinutes(3).TotalMilliseconds);
                    if (!p.HasExited)
                    {
                        p.Kill();
                        throw new ServiceControlSetupTimeoutException("Timed out waiting for ServiceControl to setup instance. This usually indicates a configuration error.");
                    }

                    if (p.ExitCode != 0)
                    {
                        throw new ServiceControlSetupFailedException($"ServiceControl.exe threw an error when setting up. This typically indicates a configuration error such as an invalid connection string. The error output from ServiceControl.exe was:\r\n {error}");
                    }
                }
                else
                {
                    throw new Exception("Attempt to launch ServiceControl.exe failed.");
                }
            }
        }
    }
}
