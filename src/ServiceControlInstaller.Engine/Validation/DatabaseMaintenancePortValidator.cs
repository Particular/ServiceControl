namespace ServiceControlInstaller.Engine.Validation
{
    using ServiceControlInstaller.Engine.Ports;

    public class DatabaseMaintenancePortValidator
    {
        public static void Validate(IServiceControlInstance instance)
        {
            if (instance.Version.Major < 2) //Maintenance port was introduced in Version 2
            {
                return;
            }

            if (!instance.DatabaseMaintenancePort.HasValue)
            {
                throw new EngineValidationException("Maintenance port number is not set");
            }

            if (instance.DatabaseMaintenancePort < 1 || instance.DatabaseMaintenancePort > 49151)
            {
                throw new EngineValidationException("Maintenance port number is not between 1 and 49151");
            }

            if (!PortUtils.CheckAvailable(instance.DatabaseMaintenancePort.Value))
                throw new EngineValidationException($"Port {instance.DatabaseMaintenancePort} is not available");
        }
    }
}