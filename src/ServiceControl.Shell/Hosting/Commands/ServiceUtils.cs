namespace Particular.ServiceControl.Commands
{
    using System.Linq;
    using System.ServiceProcess;

    static class ServiceUtils
    {
        public static bool IsServiceInstalled(string serviceName)
        {
            return ServiceController.GetServices()
                .Any(service => string.CompareOrdinal(service.ServiceName, serviceName) == 0);
        }

    }
}