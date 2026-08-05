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
            return new ReadOnlyCollection<MonitoringInstance>(services.Where(p => File.Exists(p.ExePath)).Select(p => new MonitoringInstance(p)).ToList());
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
            return new ReadOnlyCollection<ServiceControlInstance>(services.Where(p => File.Exists(p.ExePath)).Select(p => new ServiceControlInstance(p)).ToList());
        }

        public static ReadOnlyCollection<ServiceControlAuditInstance> ServiceControlAuditInstances()
        {
            var services = WindowsServiceController.FindInstancesByExe(Constants.ServiceControlAuditExe);
            return new ReadOnlyCollection<ServiceControlAuditInstance>(services.Where(p => File.Exists(p.ExePath)).Select(p => new ServiceControlAuditInstance(p)).ToList());
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
    }
}