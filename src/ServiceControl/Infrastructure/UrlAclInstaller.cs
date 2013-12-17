namespace ServiceControl.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Principal;
    using NServiceBus.Installation;
    using NServiceBus.Installation.Environments;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class UrlAclInstaller : INeedToInstallSomething<Windows>
    {
        static readonly ILog logger = LogManager.GetLogger(typeof(UrlAclInstaller));

        public void Install(string identity)
        {
            if (Environment.OSVersion.Version.Major <= 5)
            {
                logger.InfoFormat(
@"Did not attempt to grant user '{0}' HttpListener permissions since you are running an old OS. Processing will continue. 
To manually perform this action run the following command for each url from an admin console:
httpcfg set urlacl /u {{http://URL:PORT/[PATH/] | https://URL:PORT/[PATH/]}} /a D:(A;;GX;;;""{0}"")", identity);
                return;
            }
            if (!ElevateChecker.IsCurrentUserElevated())
            {
                logger.InfoFormat(
@"Did not attempt to grant user '{0}' HttpListener permissions since process is not running with elevate privileges. Processing will continue. 
To manually perform this action run the following command for each url from an admin console:
netsh http add urlacl url={{http://URL:PORT/[PATH/] | https://URL:PORT/[PATH/]}} user=""{0}""", identity);
                return;
            }

           
            //api
            RegisterUrlAcl(identity,  new Uri(Settings.ApiUrl));

            //storage
            RegisterUrlAcl(identity, new Uri(Settings.StorageUrl));
        }

        static void RegisterUrlAcl(string identity, Uri uri)
        {
            if (!uri.Scheme.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            StartNetshProcess(identity, uri);
        }

        static void StartNetshProcess(string identity, Uri uri, bool deleteExisting = true)
        {
            var startInfo = GetProcessStartInfo(identity, uri);

            string error;

            if (ExecuteNetshCommand(startInfo, out error))
            {
                logger.InfoFormat("Granted user '{0}' HttpListener permissions for {1}.", identity, uri);

                return;
            }


            if (deleteExisting && error.Contains("Error: 183"))
            {
                startInfo = GetProcessStartInfo(identity, uri, true);

                logger.Info(string.Format(@"Failed to grant to grant user '{0}' HttpListener permissions.  The error message from running the above command is: {1} Will try to delete the existing urlacl", identity, error));


                if (ExecuteNetshCommand(startInfo, out error))
                {
                    logger.InfoFormat("Deleted user '{0}' HttpListener permissions for {1}.", identity, uri);
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

        static ProcessStartInfo GetProcessStartInfo(string identity, Uri uri, bool delete = false)
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
    }

    static class ElevateChecker
    {

        public static bool IsCurrentUserElevated()
        {
            using (var windowsIdentity = WindowsIdentity.GetCurrent())
            {
                if (windowsIdentity == null)
                {
                    return false;
                }
                var windowsPrincipal = new WindowsPrincipal(windowsIdentity);
                return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}