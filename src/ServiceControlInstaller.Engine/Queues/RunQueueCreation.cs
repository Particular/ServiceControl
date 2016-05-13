namespace ServiceControlInstaller.Engine.Queues
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using ServiceControlInstaller.Engine.Accounts;
    using ServiceControlInstaller.Engine.Instances;

    internal class QueueCreation
    {
        public static void RunQueueCreation(IServiceControlInstance instance, string overrideUserName = null)
        {
            var accountName = overrideUserName ?? instance.ServiceAccount;
            var userAccount =  UserAccount.ParseAccountName(accountName);

            string args;

            if (userAccount.IsLocalSystem())
            {
                args = $"-setup --serviceName={instance.Name}";
            }
            else
            {
                args = $"-setup --serviceName={instance.Name} {userAccount.QualifiedName}";
            }

            var processStartupInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = Path.Combine(instance.InstallPath, "ServiceControl.exe"),
                Arguments = args,
                WorkingDirectory = instance.InstallPath,
                RedirectStandardError = true
            };

            var p = Process.Start(processStartupInfo);
            if (p != null)
            {
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit((int) TimeSpan.FromMinutes(1).TotalMilliseconds);
                if (!p.HasExited)
                {
                    p.Kill();
                    throw new ServiceControlQueueCreationTimeoutException("Timed out waiting for ServiceControl to created queues. This usually indicates a configuration error.");
                }

                if (p.ExitCode != 0)
                {
                    throw new ServiceControlQueueCreationFailedException($"ServiceControl.exe threw an error when creating queues. This typically indicates a configuration error such a as an invalid connection string. The error output from ServiceControl.exe was:\r\n {error}");
                }
            }
            else
            {
                throw new Exception("Attempt to launch ServiceControl.exe failed.");
            }
        }
    }
}
