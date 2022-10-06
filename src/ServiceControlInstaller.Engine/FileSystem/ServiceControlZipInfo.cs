namespace ServiceControlInstaller.Engine.FileSystem
{
    using Instances;

    public static class ServiceControlZipInfo
    {
        public static PlatformZipInfo Find(string deploymentCachePath) =>
            new PlatformZipInfoFinder("particular.servicecontrol",
                Constants.ServiceControlExe,
                "ServiceControl"
            ).Find(deploymentCachePath);
    }
}