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
                }).Where(queuename => string.Compare(queuename, "!disable", StringComparison.OrdinalIgnoreCase) != 0 &&
                                      string.Compare(queuename, "!disable.log", StringComparison.OrdinalIgnoreCase) != 0)
                .Distinct()
                .ToList();
        }

        public static List<string> UsedAuditQueueNames(TransportInfo transportInfo = null, string instanceName = null, string connectionString = null)
        {
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var instancesByTransport = serviceControlInstances.Where(p => p.TransportPackage.Equals(transportInfo) &&
                                                                          string.Equals(p.ConnectionString, connectionString, StringComparison.OrdinalIgnoreCase)).ToList();

            return instancesByTransport
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.AuditQueue,
                    p.AuditLogQueue
                }).Where(queuename => string.Compare(queuename, "!disable", StringComparison.OrdinalIgnoreCase) != 0 &&
                                      string.Compare(queuename, "!disable.log", StringComparison.OrdinalIgnoreCase) != 0)
                .Distinct()
                .ToList();
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