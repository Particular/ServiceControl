namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using NServiceBus.Logging;
    using System.Data.Common;

    static class TransportConfigurationExtensions
    {
        public static void ApplyConnectionString(this TransportExtensions<RabbitMQTransport> transport, string connectionString)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue("DisableRemoteCertificateValidation", out var disableRemoteCertificateValidationValue))
            {
                if (bool.TryParse(disableRemoteCertificateValidationValue.ToString(), out var disableRemoteCertificateValidation))
                {
                    if (disableRemoteCertificateValidation)
                    {
                        transport.DisableRemoteCertificateValidation();
                    }
                }
                else
                {
                    Logger.Warn($"Can't parse DisableRemoteCertificateValidation={disableRemoteCertificateValidationValue} as a valid boolean flag.");
                }
            }

            if (builder.TryGetValue("UseExternalAuthMechanism", out var useExternalAuthMechanismValue))
            {
                if (bool.TryParse(useExternalAuthMechanismValue.ToString(), out var useExternalAuthMechanism))
                {
                    if (useExternalAuthMechanism)
                    {
                        transport.UseExternalAuthMechanism();
                    }
                }
                else
                {
                    Logger.Warn($"Can't parse UseExternalAuthMechanism={useExternalAuthMechanismValue} as a valid boolean flag.");
                }
            }

            transport.ConnectionString(connectionString);
        }

        static ILog Logger = LogManager.GetLogger("RabbitMQTransportCustomization");
    }
}