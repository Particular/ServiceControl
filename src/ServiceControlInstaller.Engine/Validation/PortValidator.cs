namespace ServiceControlInstaller.Engine.Validation
{
    using Ports;

    public class PortValidator
    {
        public static void Validate(IHttpInstance instance)
        {
            if (instance.Port < 1 || instance.Port > 49151)
            {
                throw new EngineValidationException("Port number is not between 1 and 49151");
            }

            if (!PortUtils.CheckAvailable(instance.Port))
                throw new EngineValidationException($"Port {instance.Port} is not available");
        }
    }
}