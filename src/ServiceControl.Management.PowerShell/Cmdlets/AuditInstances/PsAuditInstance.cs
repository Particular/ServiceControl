namespace ServiceControl.Management.PowerShell
{
    using System;
    using NuGet.Versioning;
    using ServiceControlInstaller.Engine.Instances;

    public class PsAuditInstance
    {
        public string ServiceName { get; set; }

        public string InstanceName { get; set; }

        public string Url { get; set; }

        public string HostName { get; set; }

        public int Port { get; set; }

        public int? DatabaseMaintenancePort { get; set; }

        public string InstallPath { get; set; }

        public string DBPath { get; set; }

        public string LogPath { get; set; }

        public string TransportPackageName { get; set; }

        public string ConnectionString { get; set; }

        public string PersistencePackageName { get; set; }

        public string AuditQueue { get; set; }

        public string AuditLogQueue { get; set; }

        public bool ForwardAuditMessages { get; set; }

        public TimeSpan AuditRetentionPeriod { get; set; }

        public string ServiceAccount { get; set; }

        public SemanticVersion Version { get; set; }

        public string ServiceControlQueueAddress { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        public static PsAuditInstance FromInstance(ServiceControlAuditInstance instance)
            => new PsAuditInstance
            {
                ServiceName = instance.Name,
                InstanceName = instance.InstanceName,
                Url = instance.Url,
                HostName = instance.HostName,
                Port = instance.Port,
                DatabaseMaintenancePort = instance.DatabaseMaintenancePort,
                InstallPath = instance.InstallPath,
                LogPath = instance.LogPath,
                DBPath = instance.DBPath,
                TransportPackageName = instance.TransportPackage.DisplayName,
                ConnectionString = instance.ConnectionString,
                AuditQueue = instance.AuditQueue,
                AuditLogQueue = instance.AuditLogQueue,
                ForwardAuditMessages = instance.ForwardAuditMessages,
                ServiceAccount = instance.ServiceAccount,
                Version = instance.Version,
                AuditRetentionPeriod = instance.AuditRetentionPeriod,
                ServiceControlQueueAddress = instance.ServiceControlQueueAddress,
                EnableFullTextSearchOnBodies = instance.EnableFullTextSearchOnBodies,
                PersistencePackageName = instance.PersistenceManifest?.DisplayName,
            };
    }
}