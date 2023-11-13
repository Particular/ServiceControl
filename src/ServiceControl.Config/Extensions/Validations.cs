namespace ServiceControl.Config.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public static class Validations
    {
        public static List<string> UsedPaths(string instanceName = null)
        {
            var monitoringInstances = InstanceFinder.MonitoringInstances();
            var serviceControlAuditInstances = InstanceFinder.ServiceControlAuditInstances();
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var result = new List<string>();

            result.AddRange(monitoringInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.LogPath,
                    p.InstallPath
                }));

            result.AddRange(serviceControlAuditInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.LogPath,
                    p.DBPath,
                    p.InstallPath
                }));

            result.AddRange(serviceControlInstances
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
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var instancesByTransport = serviceControlInstances.Where(p => p.TransportPackage.Equals(transportInfo) &&
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
            var serviceControlInstances = InstanceFinder.ServiceControlAuditInstances();
            var instancesByTransport = serviceControlInstances.Where(p => p.TransportPackage.Equals(transportInfo) &&
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
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var port = serviceControlInstances
                    .Where(x => x.Name == instanceName)
                    .Select(x => x.Port)
                    .FirstOrDefault();

            return port;
        }

        public static int? GetErrorInstanceDatabaseMaintenancePort(string instanceName)
        {
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var port = serviceControlInstances
                .Where(x => x.Name == instanceName)
                .Select(x => x.DatabaseMaintenancePort)
                .FirstOrDefault();

            return port;
        }

        public static int? GetAuditInstancePort(string instanceName)
        {
            var serviceControlInstances = InstanceFinder.ServiceControlAuditInstances();
            var port = serviceControlInstances
                .Where(x => x.Name == instanceName)
                .Select(x => x.Port)
                .FirstOrDefault();

            return port;
        }

        public static int? GetAuditInstanceDatabaseMaintenancePort(string instanceName)
        {
            var serviceControlInstances = InstanceFinder.ServiceControlAuditInstances();
            var port = serviceControlInstances
                .Where(x => x.Name == instanceName)
                .Select(x => x.DatabaseMaintenancePort)
                .FirstOrDefault();

            return port;
        }

        public static int? GetMonitoringInstancePort(string instanceName)
        {
            var serviceControlInstances = InstanceFinder.MonitoringInstances();
            var port = serviceControlInstances
                .Where(x => x.Name == instanceName)
                .Select(x => x.Port)
                .FirstOrDefault();

            return port;
        }

        public static List<string> UsedPorts(string instanceName = null)
        {
            var monitoringInstances = InstanceFinder.MonitoringInstances();
            var serviceControlAuditInstances = InstanceFinder.ServiceControlAuditInstances();
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var result = new List<string>();

            result.AddRange(monitoringInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .Select(p => p.Port.ToString()));

            result.AddRange(serviceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.Port.ToString(),
                    p.DatabaseMaintenancePort.ToString()
                }));

            result.AddRange(serviceControlAuditInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.Port.ToString(),
                    p.DatabaseMaintenancePort.ToString()
                }));

            return result.Distinct().ToList();
        }
    }
}