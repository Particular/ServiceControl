// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.IO;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;

    public class PsServiceControl
    {
        public static PsServiceControl FromInstance(ServiceControlInstance instance)
        {
            var result = new PsServiceControl
            {
                Instance = instance
            };
            return result;
        }

        private ServiceControlInstance Instance { get; set; }
        public string Name => Instance.Name;
        public string Url => Instance.Url;
        public string HostName => Instance.HostName;
        public int Port => Instance.Port;
        public string InstallPath => Instance.InstallPath;
        public string DBPath => Instance.DBPath;
        public string BodyStoragePath => Instance.Version < SettingsList.BodyStoragePath.SupportedFrom ? null : Instance.BodyStoragePath;
        public string IngestionCachePath => Instance.Version < SettingsList.IngestionCachePath.SupportedFrom ? null : Instance.IngestionCachePath;
        public string LogPath => Instance.LogPath;
        public string TransportPackage => Instance.TransportPackage;
        public string ConnectionString => Instance.ConnectionString;
        public string ErrorQueue => Instance.ErrorQueue;
        public string ErrorLogQueue => Instance.ErrorLogQueue;
        public string AuditQueue => Instance.AuditQueue;
        public string AuditLogQueue => Instance.AuditLogQueue;
        public bool ForwardAuditMessages => Instance.ForwardAuditMessages;
        public bool ForwardErrorMessages => Instance.Version < SettingsList.ForwardErrorMessages.SupportedFrom || Instance.ForwardErrorMessages;
        public string ServiceAccount => Instance.ServiceAccount;
        public Version Version => Instance.Version;
        public string AppConfigPath => Path.Combine(Instance.InstallPath, "ServiceControl.exe.config");
        public void Refresh()
        {
            Instance.Reload();
        }
    }
}
