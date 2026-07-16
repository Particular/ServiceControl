namespace ServiceControl.Persistence.EFCore.Abstractions;

using ServiceControl.Configuration;

public abstract class EFPersistenceConfigurationBase : IPersistenceConfiguration
{
    const string ConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string MessageBodyStoragePathKey = "MessageBody/StoragePath";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";
    const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
    const string EnableFullTextSearchOnBodiesKey = "EnableFullTextSearchOnBodies";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var settings = CreateSettings(GetRequiredSetting<string>(settingsRootNamespace, ConnectionStringKey));

        settings.CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30);
        settings.MessageBodyStoragePath = SettingsReader.Read<string>(settingsRootNamespace, MessageBodyStoragePathKey);
        settings.MinBodySizeForCompression = SettingsReader.Read(settingsRootNamespace, MinBodySizeForCompressionKey, 4096);
        settings.ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey);
        settings.EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, EnableFullTextSearchOnBodiesKey, true);

        return settings;
    }

    public abstract IPersistence Create(PersistenceSettings settings);

    protected abstract EFPersisterSettings CreateSettings(string connectionString);

    static T GetRequiredSetting<T>(SettingsRootNamespace settingsRootNamespace, string key)
    {
        if (SettingsReader.TryRead<T>(settingsRootNamespace, key, out var value))
        {
            return value;
        }

        throw new Exception($"Setting {key} of type {typeof(T)} is required");
    }
}
