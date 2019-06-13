namespace ServiceControlInstaller.PowerShell
{
    using System;
    using Engine.Instances;

    public class PsAuditInstance
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

        public string AuditQueue { get; set; }
        public string AuditLogQueue { get; set; }
        public bool ForwardAuditMessages { get; set; }

        public string ServiceAccount { get; set; }

        public Version Version { get; set; }


        public static PsAuditInstance FromInstance(ServiceControlAuditInstance instance)
            => new PsAuditInstance
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
                AuditQueue = instance.AuditQueue,
                AuditLogQueue = instance.AuditLogQueue,
                ForwardAuditMessages = instance.ForwardAuditMessages,
                ServiceAccount = instance.ServiceAccount,
                Version = instance.Version,
            };
    }
}
