namespace ServiceControl.Transports.SQLServer
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ServiceControlSQLServerTransport : SqlServerTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            connectionString = connectionString.RemoveCustomSchemaPart(out var customSchema);

            if (customSchema != null)
            {
                settings.Set("SqlServer.DisableConnectionStringValidation", true);
                settings.Set("SqlServer.SchemaName", customSchema);
            }

            return base.Initialize(settings, connectionString);
        }
    }
}
