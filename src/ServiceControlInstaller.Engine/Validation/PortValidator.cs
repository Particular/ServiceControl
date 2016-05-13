namespace ServiceControlInstaller.Engine.Validation
{
    using ServiceControlInstaller.Engine.Ports;

    public class PortValidator
    {
        public static void Validate(IContainPort instance)
        {
            if ((1 > instance.Port) || (49151 < instance.Port))
            {
                throw new EngineValidationException("Port number is not between 1 and 65535");
            }

            if (!PortUtils.CheckAvailable(instance.Port))
                throw new EngineValidationException($"Port {instance.Port} is not available");
        }
    }
}
