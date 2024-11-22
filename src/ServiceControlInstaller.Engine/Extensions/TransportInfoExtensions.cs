namespace ServiceControl.Engine.Extensions
{
    using ServiceControlInstaller.Engine.Instances;

    public static class TransportInfoExtensions
    {
        public static bool IsLatestRabbitMQTransport(this TransportInfo transport)
        {
            return transport.ZipName == "RabbitMQ" && transport.AvailableInSCMU;
        }

        public static bool IsOldRabbitMQTransport(this TransportInfo transport)
        {
            return transport.ZipName == "RabbitMQ" && !transport.AvailableInSCMU;
        }
    }
}