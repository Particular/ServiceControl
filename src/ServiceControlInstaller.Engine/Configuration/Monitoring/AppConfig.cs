namespace ServiceControlInstaller.Engine.Configuration.Monitoring
{
    using System;
    using System.IO;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public class AppConfig : AppConfigWrapper
    {
        IMonitoringInstance details;
        
        public AppConfig(IMonitoringInstance details) : base(Path.Combine(details.InstallPath, $"{Constants.MonitoringExe}.config"))
        {
            this.details = details;
        }

        public void Validate()
        {
            if (V6Transports.FindByName(details.TransportPackage) == null)
            {
                throw new Exception($"Invalid Transport - Must be one of: {string.Join(",", V6Transports.All.Select(p => p.Name))}");
            }
        }

        public void Save()
        {
            Config.ConnectionStrings.ConnectionStrings.Set("NServiceBus/Transport", details.ConnectionString);
            var settings = Config.AppSettings.Settings;
            var version = details.Version;
            settings.Set(SettingsList.Port, details.Port.ToString());
            settings.Set(SettingsList.HostName, details.HostName);
            settings.Set(SettingsList.LogPath, details.LogPath);
            settings.Set(SettingsList.TransportType, V6Transports.FindByName(details.TransportPackage).TypeName, version);
            settings.Set(SettingsList.ErrorQueue, details.ErrorQueue);
            Config.Save();
        }
    }
}
