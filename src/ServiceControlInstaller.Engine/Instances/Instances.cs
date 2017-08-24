namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using ServiceControlInstaller.Engine.Services;

    public class InstanceFinder
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

        public static ServiceControlInstance FindServiceControlInstance(string instanceName)
        {
            try
            {
                return ServiceControlInstances().Single(p => p.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
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
            services.AddRange(MonitoringInstances());
            return new ReadOnlyCollection<BaseService>(services.OrderBy(o => o.Name).ToList());
        }
    }
}
