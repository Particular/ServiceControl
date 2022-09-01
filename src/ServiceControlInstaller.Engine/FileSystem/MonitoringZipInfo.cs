namespace ServiceControlInstaller.Engine.FileSystem
{
    using Instances;

    public static class MonitoringZipInfo
    {
        /// <summary>
        /// Find the latest servicecontrol zip based on the version number in the file name - file name must be in form
        /// particular.servicecontrol-&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;.zip
        /// </summary>
        public static PlatformZipInfo Find(string deploymentCachePath) =>
            new PlatformZipInfoFinder(
                "particular.servicecontrol.monitoring",
                Constants.MonitoringExe,
                "ServiceControl Monitoring"
            ).Find(deploymentCachePath);
    }
}