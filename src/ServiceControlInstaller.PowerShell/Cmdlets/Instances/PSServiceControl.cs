// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    public class PsServiceControl :  IContainPort, IContainInstancePaths, IContainTransportInfo
    {
        public static PsServiceControl FromInstance(ServiceControlInstance instance)
        {
            var result = new PsServiceControl
            {
                Name = instance.Name,
                Url = instance.Url,
                HostName = instance.HostName,
                Port = instance.Port,
                InstallPath = instance.InstallPath,
                IngestionCachePath = instance.IngestionCachePath,
                BodyStoragePath = instance.BodyStoragePath,
                LogPath = instance.LogPath,
                DBPath = instance.DBPath,
                TransportPackage = instance.TransportPackage,
                ConnectionString = instance.ConnectionString,
                AuditQueue = instance.AuditQueue,
                AuditLogQueue = instance.AuditLogQueue,
                ErrorQueue = instance.ErrorQueue,
                ErrorLogQueue = instance.ErrorLogQueue,
                ForwardAuditMessages = instance.ForwardAuditMessages,
                ServiceAccount = instance.ServiceAccount,
                Version = instance.Version,
                ForwardErrorMessages = instance.Version < SettingsList.ForwardErrorMessages.SupportedFrom || instance.ForwardErrorMessages
            };
            return result;
        }
        public string Name { get; set; }
        public string Url { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }

        public string InstallPath { get; set; }
        public string DBPath { get; set; }
        public string BodyStoragePath { get; set; }
        public string IngestionCachePath { get; set; }
        public string LogPath { get; set; }

        public string TransportPackage { get;  set; }
        public string ConnectionString { get; set; }

        public string ErrorQueue { get;  set; }
        public string ErrorLogQueue { get; set; }
        
        public string AuditQueue { get;  set; }
        public string AuditLogQueue { get;  set; }
        public bool ForwardAuditMessages { get; set; }
        public bool ForwardErrorMessages { get; set; }

        public string ServiceAccount { get; set; }

        public Version Version { get; set; }
        
    }
}
