namespace ServiceBus.Management.Infrastructure.Installers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus.Installation;
    using NServiceBus.Installation.Environments;
    using NServiceBus.Logging;
    using Settings;

    public class UrlAclInstaller : INeedToInstallSomething<Windows>
    {
        public void Install(string identity)
        {
            if (Environment.OSVersion.Version.Major <= 5)
            {
                Logger.InfoFormat(
                    @"Did not attempt to grant user '{0}' HttpListener permissions since you are running an old OS. Processing will continue. 
To manually perform this action run the following command for each url from an admin console:
httpcfg set urlacl /u {{http://URL:PORT/[PATH/] | https://URL:PORT/[PATH/]}} /a D:(A;;GX;;;""{0}"")", identity);
                return;
            }

            StartNetshProcess(identity, Settings.ApiUrl);
        }

        static void StartNetshProcess(string identity, string uri, bool deleteExisting = true)
        {
            var startInfo = GetProcessStartInfo(identity, uri);

            string error;

            if (ExecuteNetshCommand(startInfo, out error))
            {
                Logger.InfoFormat("Granted user '{0}' HttpListener permissions for {1}.", identity, uri);

                return;
            }

            if (deleteExisting && error.Contains("Error: 183"))
            {
                startInfo = GetProcessStartInfo(identity, uri, true);

                Logger.Info(
                    string.Format(
                        @"Failed to grant to grant user '{0}' HttpListener permissions.  The error message from running the above command is: {1} Will try to delete the existing urlacl",
                        identity, error));

                if (ExecuteNetshCommand(startInfo, out error))
                {
                    Logger.InfoFormat("Deleted user '{0}' HttpListener permissions for {1}.", identity, uri);
                    StartNetshProcess(identity, uri, false);
                    return;
                }
            }

            throw new Exception(string.Format(
                @"Failed to grant to grant user '{0}' HttpListener permissions.
Try running the following command from an admin console:
netsh http add urlacl url={2} user=""{0}""

The error message from running the above command is: 
{1}", identity, error, uri));
        }

        static bool ExecuteNetshCommand(ProcessStartInfo startInfo, out string error)
        {
            error = null;

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    return true;
                }

                error = process.StandardOutput.ReadToEnd().Trim();

                return false;
            }
        }

        static ProcessStartInfo GetProcessStartInfo(string identity, string uri, bool delete = false)
        {
            var arguments = string.Format(@"http {1} urlacl url={0}", uri, delete ? "delete" : "add");

            if (!delete)
            {
                arguments += string.Format(" user=\"{0}\"", identity);
            }

            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                Verb = "runas",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = arguments,
                FileName = "netsh",
                WorkingDirectory = Path.GetTempPath()
            };
            return startInfo;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(UrlAclInstaller));
    }
}