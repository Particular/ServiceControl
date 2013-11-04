namespace ServiceControl.EndpointPlugin.Operations.ServiceControlBackend
{
    using System;
    using System.Diagnostics;
    using NServiceBus;

    internal class VersionChecker
    {
        static VersionChecker()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(typeof(IMessage).Assembly.Location);

            CoreFileVersion = new Version(fileVersion.FileMajorPart,fileVersion.FileMinorPart,fileVersion.FileBuildPart);
        }

        public static bool CoreVersionIsAtLeast(int major, int minor)
        {
            if (CoreFileVersion.Major > major)
                return true;

            if (CoreFileVersion.Major < major)
                return false;

            return CoreFileVersion.Minor >= minor;
        }

        public static Version CoreFileVersion { get; set; }
    }
}