namespace ServiceBus.Management.Infrastructure.Settings;

using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

class ConfigureSettings(
    IOptions<LoggingOptions> logging,
    IOptions<PrimaryOptions> primary,
    IOptions<ServiceBusOptions> serviceBus,
    PersistenceSettings persistenceSettings
) : IConfigureOptions<Settings>
{
    public void Configure(Settings options)
    {
        options.Logging = logging.Value;
        options.ServiceBus = serviceBus.Value;
        options.ServiceControl = primary.Value;
        options.PersisterSpecificSettings = persistenceSettings;
    }
}