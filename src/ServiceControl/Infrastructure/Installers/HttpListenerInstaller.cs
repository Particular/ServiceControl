namespace ServiceControl.Infrastructure.Installers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus.Installation;
    using NServiceBus.Logging;
    using Settings;

    public class HttpListenerInstaller : INeedToInstallSomething
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

            StartNetshProcess(identity, new Uri(Settings.ApiUrl));
        }

        static void StartNetshProcess(string identity, Uri uri)
        {
            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                Verb = "runas",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = string.Format(@"http add urlacl url={0} user=""{1}""", uri, identity),
                FileName = "netsh",
                WorkingDirectory = Path.GetTempPath()
            };
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    Logger.InfoFormat("Granted user '{0}' HttpListener permissions for {1}.", identity, uri);
                    return;
                }
                var error = process.StandardOutput.ReadToEnd().Trim();
                var message = string.Format(
                    @"Failed to grant to grant user '{0}' HttpListener permissions. Processing will continue. 
Try running the following command from an admin console:
netsh http add urlacl url={2} user=""{0}""

The error message from running the above command is: 
{1}", identity, error, uri);
                Logger.Warn(message);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(HttpListenerInstaller));
    }
}