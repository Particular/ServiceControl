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
                args = string.Format("-setup --serviceName={0}", instance.Name);
            }
            else
            {
                args = string.Format("-setup --serviceName={0} {1}", instance.Name, userAccount.QualifiedName);
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

                p.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds);

                if (!p.HasExited)
                {
                    throw new Exception("Timed out waiting for ServiceControl to created queues");
                }

                if (p.ExitCode != 0)
                {
                    throw new Exception(string.Format("ServiceControl instance failed to run and created queues. {0}", error));
                }
            }
        }
    }
}
