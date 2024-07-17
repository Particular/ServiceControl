namespace ServiceControl.Management.PowerShell.Cmdlets.Instances
{
    using NuGet.Versioning;
    using ServiceControlInstaller.Engine.Instances;

    public class PsMonitoringService
    {
        public string ServiceName { get; set; }

        public string InstanceName { get; set; }

        public string Url { get; set; }

        public string HostName { get; set; }

        public int Port { get; set; }

        public string InstallPath { get; set; }

        public string LogPath { get; set; }

        public string TransportPackageName { get; set; }

        public string ConnectionString { get; set; }

        public string ErrorQueue { get; set; }

        public string ServiceAccount { get; set; }

        public SemanticVersion Version { get; set; }

        public static PsMonitoringService FromInstance(MonitoringInstance instance)
        {
            var result = new PsMonitoringService
            {
                ServiceName = instance.Name,
                InstanceName = instance.InstanceName,
                Url = instance.Url,
                HostName = instance.HostName,
                Port = instance.Port,
                InstallPath = instance.InstallPath,
                LogPath = instance.LogPath,
                TransportPackageName = instance.TransportPackage.DisplayName,
                ConnectionString = instance.ConnectionString,
                ErrorQueue = instance.ErrorQueue,
                ServiceAccount = instance.ServiceAccount,
                Version = instance.Version
            };
            return result;
        }
    }
}