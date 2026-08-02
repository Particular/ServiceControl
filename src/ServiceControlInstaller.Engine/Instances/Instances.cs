namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using Services;

    public static class InstanceFinder
    {
        public static ReadOnlyCollection<MonitoringInstance> MonitoringInstances()
        {
            var services = WindowsServiceController.FindInstancesByExe(Constants.MonitoringExe);
            var instances = new List<MonitoringInstance>();

            foreach (var service in services.Where(p => File.Exists(p.ExePath)))
            {
                try
                {
                    var instance = new MonitoringInstance(service);
                    instances.Add(instance);
                }
                catch (Exception ex)
                {
                    // Log the error but continue loading other instances
                    LogInstanceLoadError("Monitoring", service.ServiceName, ex);
                }
            }

            return new ReadOnlyCollection<MonitoringInstance>(instances);
        }

        public static MonitoringInstance FindMonitoringInstance(string instanceName)
        {
            try
            {
                return MonitoringInstances().Single(p => p.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new Exception("Instance does not exists", ex);
            }
        }

        public static ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances()
        {
            var services = WindowsServiceController.FindInstancesByExe(Constants.ServiceControlExe);
            var instances = new List<ServiceControlInstance>();

            foreach (var service in services.Where(p => File.Exists(p.ExePath)))
            {
                try
                {
                    var instance = new ServiceControlInstance(service);
                    instances.Add(instance);
                }
                catch (Exception ex)
                {
                    // Log the error but continue loading other instances
                    LogInstanceLoadError("ServiceControl", service.ServiceName, ex);
                }
            }

            return new ReadOnlyCollection<ServiceControlInstance>(instances);
        }

        public static ReadOnlyCollection<ServiceControlAuditInstance> ServiceControlAuditInstances()
        {
            var services = WindowsServiceController.FindInstancesByExe(Constants.ServiceControlAuditExe);
            var instances = new List<ServiceControlAuditInstance>();

            foreach (var service in services.Where(p => File.Exists(p.ExePath)))
            {
                try
                {
                    var instance = new ServiceControlAuditInstance(service);
                    instances.Add(instance);
                }
                catch (Exception ex)
                {
                    // Log the error but continue loading other instances
                    LogInstanceLoadError("Audit", service.ServiceName, ex);
                }
            }

            return new ReadOnlyCollection<ServiceControlAuditInstance>(instances);
        }

        public static T FindInstanceByName<T>(string instanceName) where T : ServiceControlBaseService
        {
            try
            {
                var instances = ServiceControlInstances().Cast<ServiceControlBaseService>().Union(ServiceControlAuditInstances());
                return (T)instances.Single(p => p.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new Exception("Instance does not exists", ex);
            }
        }

        public static ServiceControlBaseService FindServiceControlInstance(string instanceName)
        {
            try
            {
                var instances = ServiceControlInstances().Cast<ServiceControlBaseService>().Union(ServiceControlAuditInstances());
                return instances.Single(p => p.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new Exception("Instance does not exists", ex);
            }
        }

        public static ReadOnlyCollection<BaseService> AllInstances()
        {
            var services = new List<BaseService>();
            services.AddRange(ServiceControlInstances());
            services.AddRange(ServiceControlAuditInstances());
            services.AddRange(MonitoringInstances());
            return new ReadOnlyCollection<BaseService>(services.OrderBy(o => o.Name).ToList());
        }

        static void LogInstanceLoadError(string instanceType, string serviceName, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "ServiceControl", "SCMU_Logs");
                Directory.CreateDirectory(logPath);

                var logFile = Path.Combine(logPath, "InstanceLoadErrors.log");
                var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Failed to load {instanceType} instance '{serviceName}':\r\n{ex}\r\n\r\n";

                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // If logging fails, just continue - the error is already captured in the instance's ReportCard
                System.Diagnostics.Debug.WriteLine($"Failed to write to instance load error log: {ex.Message}");
            }
        }
    }
}