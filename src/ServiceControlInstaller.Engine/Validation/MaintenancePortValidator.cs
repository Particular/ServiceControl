namespace ServiceControlInstaller.Engine.Validation
{
    using ServiceControlInstaller.Engine.Ports;

    public class MaintenancePortValidator
    {
        public static void Validate(IServiceControlInstance instance)
        {
            if (instance.MaintenancePort < 1 || instance.MaintenancePort > 49151)
            {
                throw new EngineValidationException("Port number is not between 1 and 49151");
            }

            if (!PortUtils.CheckAvailable(instance.MaintenancePort))
                throw new EngineValidationException($"Port {instance.MaintenancePort} is not available");
        }
    }
}