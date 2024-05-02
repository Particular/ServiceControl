namespace ServiceControl.Config.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public static class Validations
    {
        public static List<string> UsedPaths(string instanceName = null)
        {
            var result = new List<string>();

            result.AddRange(monitoringInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.LogPath,
                    p.InstallPath
                }));

            result.AddRange(serviceControlAuditInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.LogPath,
                    p.DBPath,
                    p.InstallPath
                }));

            result.AddRange(serviceControlInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.LogPath,
                    p.DBPath,
                    p.InstallPath
                }));

            return result.Distinct().ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        public static List<string> UsedErrorQueueNames(TransportInfo transportInfo = null, string instanceName = null, string connectionString = null)
        {
            var instancesByTransport = serviceControlInstances.Value.Where(p => p.TransportPackage.Equals(transportInfo) &&
                                                                          string.Equals(p.ConnectionString, connectionString, StringComparison.OrdinalIgnoreCase)).ToList();

            return instancesByTransport
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.ErrorLogQueue,
                    p.ErrorQueue
                }).Where(queuename => !string.IsNullOrEmpty(queuename))
                .Distinct()
                .ToList();
        }

        public static List<string> UsedAuditQueueNames(TransportInfo transportInfo = null, string instanceName = null, string connectionString = null)
        {
            var instancesByTransport = serviceControlInstances.Value.Where(p => p.TransportPackage.Equals(transportInfo) &&
                                                                          string.Equals(p.ConnectionString, connectionString, StringComparison.OrdinalIgnoreCase)).ToList();

            return instancesByTransport
                 .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                 .SelectMany(p => new[]
                 {
                    p.AuditQueue,
                    p.AuditLogQueue
                 }).Where(queuename => !string.IsNullOrEmpty(queuename))
                 .Distinct()
                 .ToList();
        }

        public static int? GetErrorInstancePort(string instanceName)
        {
            var port = serviceControlInstances.Value
                    .Where(x => x.Name == instanceName)
                    .Select(x => x.Port)
                    .FirstOrDefault();

            return port;
        }

        public static int? GetErrorInstanceDatabaseMaintenancePort(string instanceName)
        {
            var port = serviceControlInstances.Value
                .Where(x => x.Name == instanceName)
                .Select(x => x.DatabaseMaintenancePort)
                .FirstOrDefault();

            return port;
        }

        public static int? GetAuditInstancePort(string instanceName)
        {
            var port = serviceControlInstances.Value
                .Where(x => x.Name == instanceName)
                .Select(x => x.Port)
                .FirstOrDefault();

            return port;
        }

        public static int? GetAuditInstanceDatabaseMaintenancePort(string instanceName)
        {
            var port = serviceControlInstances.Value
                .Where(x => x.Name == instanceName)
                .Select(x => x.DatabaseMaintenancePort)
                .FirstOrDefault();

            return port;
        }

        public static int? GetMonitoringInstancePort(string instanceName)
        {
            var port = serviceControlInstances.Value
                .Where(x => x.Name == instanceName)
                .Select(x => x.Port)
                .FirstOrDefault();

            return port;
        }

        public static List<string> UsedPorts(string instanceName = null)
        {
            var result = new List<string>();

            result.AddRange(monitoringInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .Select(p => p.Port.ToString()));

            result.AddRange(serviceControlInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.Port.ToString(),
                    p.DatabaseMaintenancePort.ToString()
                }));

            result.AddRange(serviceControlAuditInstances.Value
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.Port.ToString(),
                    p.DatabaseMaintenancePort.ToString()
                }));

            return result.Distinct().ToList();
        }

        public static void RefreshInstances()
        {
            monitoringInstances = new Lazy<ReadOnlyCollection<MonitoringInstance>>(() => InstanceFinder.MonitoringInstances());
            serviceControlAuditInstances = new Lazy<ReadOnlyCollection<ServiceControlAuditInstance>>(() => InstanceFinder.ServiceControlAuditInstances());
            serviceControlInstances = new Lazy<ReadOnlyCollection<ServiceControlInstance>>(() => InstanceFinder.ServiceControlInstances());
        }

        static Lazy<ReadOnlyCollection<MonitoringInstance>> monitoringInstances = new Lazy<ReadOnlyCollection<MonitoringInstance>>(() => InstanceFinder.MonitoringInstances());
        static Lazy<ReadOnlyCollection<ServiceControlAuditInstance>> serviceControlAuditInstances = new Lazy<ReadOnlyCollection<ServiceControlAuditInstance>>(() => InstanceFinder.ServiceControlAuditInstances());
        static Lazy<ReadOnlyCollection<ServiceControlInstance>> serviceControlInstances = new Lazy<ReadOnlyCollection<ServiceControlInstance>>(() => InstanceFinder.ServiceControlInstances());
    }
}