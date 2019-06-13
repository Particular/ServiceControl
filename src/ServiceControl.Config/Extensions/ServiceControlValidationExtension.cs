namespace ServiceControl.Config.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public static class ServiceControlValidationExtension
    {
        // We need this to ignore the instance that represents the edit screen
        public static List<string> UsedPaths(this ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances, string instanceName = null)
        {
            return ServiceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.DBPath,
                    p.LogPath,
                    p.InstallPath
                })
                .Distinct()
                .ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        public static List<string> UsedQueueNames(this ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances, TransportInfo transportInfo = null, string instanceName = null, string connectionString = null)
        {
            var instancesByTransport = ServiceControlInstances.Where(p => p.TransportPackage.Equals(transportInfo) &&
                                                                          string.Equals(p.ConnectionString, connectionString, StringComparison.OrdinalIgnoreCase)).ToList();

            return instancesByTransport
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.ErrorLogQueue,
                    p.ErrorQueue,
                    p.AuditQueue,
                    p.AuditLogQueue
                }).Where(queuename => string.Compare(queuename, "!disable", StringComparison.OrdinalIgnoreCase) != 0 &&
                                      string.Compare(queuename, "!disable.log", StringComparison.OrdinalIgnoreCase) != 0)
                .Distinct()
                .ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        public static List<string> UsedPorts(this ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances, string instanceName = null)
        {
            return ServiceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.Port.ToString(),
                    p.DatabaseMaintenancePort.ToString()
                })
                .Distinct()
                .ToList();
        }
    }
}