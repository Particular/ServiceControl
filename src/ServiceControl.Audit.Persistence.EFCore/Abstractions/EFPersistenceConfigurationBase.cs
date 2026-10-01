namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;

public abstract class EFPersistenceConfigurationBase : IPersistenceConfiguration
{
    public const string ConnectionStringKey = "Database/ConnectionString";
    public const string SchemaKey = "Database/Schema";
    public const string CommandTimeoutKey = "Database/CommandTimeout";
    public const string QueryTimeoutInSecondsKey = QueryTimeLimit.SettingName;
    internal const string QueryTimeoutSettingName = "ServiceControl.Audit/" + QueryTimeLimit.SettingName;

    public bool SupportsMaintenanceMode => false;

    public abstract string Name { get; }

    public IEnumerable<string> ConfigurationKeys => [ConnectionStringKey, SchemaKey, CommandTimeoutKey, QueryTimeoutInSecondsKey];

    public IPersistence Create(PersistenceSettings settings) => Create(CreateSettings(settings));

    protected abstract IPersistence Create(EFPersisterSettings settings);

    public static EFPersisterSettings CreateSettings(PersistenceSettings settings)
    {
        var specific = settings.PersisterSpecificSettings;

        if (!specific.TryGetValue(ConnectionStringKey, out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"{ConnectionStringKey} must be specified.");
        }

        return new EFPersisterSettings
        {
            ConnectionString = connectionString,
            Schema = ReadSchema(specific),
            CommandTimeout = ReadInt(specific, CommandTimeoutKey, EFPersisterSettings.DefaultCommandTimeout),
            QueryTimeout = QueryTimeLimit.Validate(ReadInt(specific, QueryTimeoutInSecondsKey, QueryTimeLimit.DefaultSeconds), QueryTimeoutSettingName, logger),
            AuditRetentionPeriod = settings.AuditRetentionPeriod,
            MaxBodySizeToStore = settings.MaxBodySizeToStore
        };
    }

    static string? ReadSchema(IDictionary<string, string> specific)
    {
        if (!specific.TryGetValue(SchemaKey, out var schema) || string.IsNullOrWhiteSpace(schema))
        {
            return null;
        }

        try
        {
            return SchemaName.Validate(schema);
        }
        catch (ArgumentException e)
        {
            throw new InvalidOperationException($"{SchemaKey} is invalid. {e.Message}", e);
        }
    }

    static int ReadInt(IDictionary<string, string> specific, string key, int defaultValue)
    {
        if (!specific.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{key} must be an integer.");
    }

    static readonly ILogger logger = LoggerUtil.CreateStaticLogger<EFPersistenceConfigurationBase>();
}
