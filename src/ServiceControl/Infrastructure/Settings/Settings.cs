namespace ServiceBus.Management.Infrastructure.Settings;

using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

public class Settings
{
    public PrimaryOptions ServiceControl { get; set; } = new();
    public ServiceBusOptions ServiceBus { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
    public PersistenceSettings PersisterSpecificSettings { get; set; }
}