// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Linq;
    using Engine.Configuration.ServiceControl;
    using Engine.Instances;

    public class PsServiceControl
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public int? DatabaseMaintenancePort { get; set; }

        public string InstallPath { get; set; }
        public string DBPath { get; set; }
        public string LogPath { get; set; }

        public string TransportPackageName { get; set; }
        public string ConnectionString { get; set; }

        public string ErrorQueue { get; set; }
        public string ErrorLogQueue { get; set; }
        public bool ForwardErrorMessages { get; set; }
        
        public TimeSpan ErrorRetentionPeriod { get; set; }

        public string AuditQueue { get; set; }
        
        public string AuditLogQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }
        
        public TimeSpan? AuditRetentionPeriod { get; set; }

        public string ServiceAccount { get; set; }

        public Version Version { get; set; }

        public object[] RemoteInstances { get; set; }

        public static PsServiceControl FromInstance(ServiceControlInstance instance)
        {
            var result = new PsServiceControl
            {
                Name = instance.Name,
                Url = instance.Url,
                HostName = instance.HostName,
                Port = instance.Port,
                DatabaseMaintenancePort = instance.DatabaseMaintenancePort,
                InstallPath = instance.InstallPath,
                LogPath = instance.LogPath,
                DBPath = instance.DBPath,
                TransportPackageName = instance.TransportPackage.Name,
                ConnectionString = instance.ConnectionString,
                ErrorQueue = instance.ErrorQueue,
                ErrorLogQueue = instance.ErrorLogQueue,
                ServiceAccount = instance.ServiceAccount,
                Version = instance.Version,
                //TODO: Do we need the version check here?
                ForwardErrorMessages = instance.Version < ServiceControlSettings.ForwardErrorMessages.SupportedFrom || instance.ForwardErrorMessages,
                AuditQueue = instance.AuditQueue,
                AuditLogQueue = instance.AuditLogQueue,
                ForwardAuditMessages = instance.ForwardAuditMessages,
                AuditRetentionPeriod = instance.AuditRetentionPeriod,
                ErrorRetentionPeriod = instance.ErrorRetentionPeriod,
                RemoteInstances = instance.RemoteInstances.Select<RemoteInstanceSetting, object>(i =>
                {
                    if (string.IsNullOrEmpty(i.QueueAddress))
                    {
                        return new { ApiUri = i.ApiUri };
                    }
                    return new { ApiUri = i.ApiUri, QueueAddress = i.QueueAddress};
                }).ToArray()
            };
            return result;
        }
    }
}