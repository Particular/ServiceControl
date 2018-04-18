namespace ServiceControlInstaller.Engine.Validation
{
    using ServiceControlInstaller.Engine.Ports;

    public class DatabaseMaintenancePortValidator
    {
        public static void Validate(IServiceControlInstance instance)
        {
            if (instance.DatabaseMaintenancePort < 1 || instance.DatabaseMaintenancePort > 49151)
            {
                throw new EngineValidationException("Port number is not between 1 and 49151");
            }

            if (!PortUtils.CheckAvailable(instance.DatabaseMaintenancePort))
                throw new EngineValidationException($"Port {instance.DatabaseMaintenancePort} is not available");
        }
    }
}