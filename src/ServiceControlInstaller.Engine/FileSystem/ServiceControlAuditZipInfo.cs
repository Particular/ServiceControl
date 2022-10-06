namespace ServiceControlInstaller.Engine.FileSystem
{
    using Instances;

    public static class ServiceControlAuditZipInfo
    {
        public static PlatformZipInfo Find(string deploymentCachePath) =>
            new PlatformZipInfoFinder(
                "particular.servicecontrol.audit",
                Constants.ServiceControlAuditExe,
                "ServiceControl Audit"
            ).Find(deploymentCachePath);
    }
}