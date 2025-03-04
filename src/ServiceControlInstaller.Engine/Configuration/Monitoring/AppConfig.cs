namespace ServiceControlInstaller.Engine.Configuration.Monitoring
{
    using System.IO;
    using Instances;

    public class AppConfig : AppConfigWrapper
    {
        public AppConfig(IMonitoringInstance details) : base(Path.Combine(details.InstallPath, $"{Constants.MonitoringExe}.config"))
        {
            this.details = details;
        }

        public void Save()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(SettingsList.InstanceName, details.InstanceName, version);
            settings.Set(SettingsList.Port, details.Port.ToString());
            settings.Set(SettingsList.HostName, details.HostName);
            settings.Set(SettingsList.LogPath, details.LogPath);
            settings.Set(SettingsList.TransportType, details.TransportPackage.Name, version);
            settings.Set(SettingsList.ErrorQueue, details.ErrorQueue);

            // Windows services allow a maximum of 125 seconds when stopping a service.
            // When shutting down or restarting the OS we have no control over the
            // shutdown timeout. This is by the installer engine that is run _only_ on
            // Windows via SCMU or PowerShell
            settings.Set(SettingsList.ShutdownTimeout, "00:02:00", version);

            // Retired settings
            settings.RemoveIfRetired(SettingsList.EndpointName, version);

            Config.Save();
        }

        IMonitoringInstance details;
    }
}