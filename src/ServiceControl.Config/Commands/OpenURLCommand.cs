namespace ServiceControl.Config.Commands
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using ServiceControl.Config.Framework.Commands;

    class OpenURLCommand : AbstractCommand<string>
    {
        public OpenURLCommand() : base(IsValidUrl)
        {
        }

        static bool IsValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))  return true;

            url = FixUrlOn64bitSystem(url);

            return !uri.IsFile || (File.Exists(url) || Directory.Exists(url));
        }

        static string FixUrlOn64bitSystem(string url)
        {
            if (!Environment.Is64BitOperatingSystem || Environment.Is64BitProcess)
            {
                return url;
            }

            var system32Directory = Path.Combine(Environment.ExpandEnvironmentVariables("%windir%"), "system32");
            // For 32-bit processes on 64-bit systems, %windir%\system32 folder
            // can only be accessed by specifying %windir%\sysnative folder.
            var systemNativeDirectory = Path.Combine(Environment.ExpandEnvironmentVariables("%windir%"), "sysnative");


            return url.Replace(system32Directory, systemNativeDirectory);
        }

        public override void Execute(string url)
        {
            var uri = new Uri(url);
            if (uri.IsFile & !Directory.Exists(uri.LocalPath))
            {
                MessageBox.Show("Unable to open the directory in Windows Explorer. The directory does not exist or access is denied", "Directory not available", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                FileName = FixUrlOn64bitSystem(url)
            });
        }
    }
}