namespace ServiceControlInstaller.Engine.Queues
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;

    internal class QueueCreation
    {
        public static void RunQueueCreation(IServiceControlInstance instance)
        {
            var accountName = instance.ServiceAccount;
            RunQueueCreation(instance.InstallPath,
                Constants.ServiceControlExe,
                instance.Name,
                accountName, 
                instance.SkipQueueCreation);
        }

        public static void RunQueueCreation(IMonitoringInstance instance)
        {
            var accountName = instance.ServiceAccount;
            RunQueueCreation(instance.InstallPath,
                Constants.MonitoringExe,
                instance.Name,
                accountName, 
                instance.SkipQueueCreation);
        }

        static void RunQueueCreation(string installPath, string exeName, string serviceName, string serviceAccount, bool skipQueueCreation = false)
        {
            var userAccount = UserAccount.ParseAccountName(serviceAccount);

            string args = $"--setup --serviceName={serviceName}";

            if (!userAccount.IsLocalSystem())
            {
                args += $" --userName=\"{userAccount.QualifiedName}\"";
            }

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

            var p = Process.Start(processStartupInfo);
            if (p != null)
            {
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                if (!p.HasExited)
                {
                    p.Kill();
                    throw new QueueCreationTimeoutException($"Timed out waiting for {exeName} to created queues. This usually indicates a configuration error.");
                }

                if (p.ExitCode != 0)
                {
                    throw new QueueCreationFailedException($"{exeName} threw an error when creating queues. This typically indicates a configuration error. The error output from {exeName} was:\r\n {error}");
                }
            }
            else
            {
                throw new Exception($"Attempt to launch {exeName} failed.");
            }
        }
    }
}