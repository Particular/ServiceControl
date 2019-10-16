namespace ServiceControl.Transports.RabbitMQ
{
    using NServiceBus;
    using System;
    using System.Data.Common;

    static class TransportConfigurationExtensions
    {
        public static void ApplyConnectionString(this TransportExtensions<RabbitMQTransport> transport, string connectionString)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.GetBooleanValue("DisableRemoteCertificateValidation"))
            {
                transport.DisableRemoteCertificateValidation();
            }

            if (builder.GetBooleanValue("UseExternalAuthMechanism"))
            {
                transport.UseExternalAuthMechanism();
            }

            transport.ConnectionString(connectionString);
        }

        public static bool GetBooleanValue(this DbConnectionStringBuilder dbConnectionStringBuilder, string key)
        {
            if (!dbConnectionStringBuilder.TryGetValue(key, out var rawValue))
            {
                return false;
            }

            if (!bool.TryParse(rawValue.ToString(), out var value))
            {
                throw new Exception($"Can't parse key '{key}'. '{rawValue}' is not a valid boolean value.");
            }

            return value;
        }
    }
}