namespace ServiceControl.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

public abstract class EFPersistenceConfigurationBase : IPersistenceConfiguration
{
    const string ConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string MessageBodyStoragePathKey = "MessageBody/StoragePath";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";
    const string MaxBodySizeToStoreKey = "MaxBodySizeToStore";
    const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
    const string EnableFullTextSearchOnBodiesKey = "EnableFullTextSearchOnBodies";

    const int MaxBodySizeToStoreDefault = 102400; //100 kb

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var settings = CreateSettings(GetRequiredSetting<string>(settingsRootNamespace, ConnectionStringKey));

        settings.CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, 30);
        settings.MessageBodyStoragePath = SettingsReader.Read<string>(settingsRootNamespace, MessageBodyStoragePathKey);
        settings.MinBodySizeForCompression = SettingsReader.Read(settingsRootNamespace, MinBodySizeForCompressionKey, 4096);
        settings.MaxBodySizeToStore = ReadMaxBodySizeToStore(settingsRootNamespace);
        settings.ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey);
        settings.EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, EnableFullTextSearchOnBodiesKey, true);

        return settings;
    }

    public abstract IPersistence Create(PersistenceSettings settings);

    protected abstract EFPersisterSettings CreateSettings(string connectionString);

    static int ReadMaxBodySizeToStore(SettingsRootNamespace settingsRootNamespace)
    {
        var maxBodySizeToStore = SettingsReader.Read(settingsRootNamespace, MaxBodySizeToStoreKey, MaxBodySizeToStoreDefault);

        if (maxBodySizeToStore <= 0)
        {
            LoggerUtil.CreateStaticLogger<EFPersistenceConfigurationBase>()
                .LogError("MaxBodySizeToStore setting is invalid, 1 is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}", MaxBodySizeToStoreDefault);

            return MaxBodySizeToStoreDefault;
        }

        return maxBodySizeToStore;
    }

    static T GetRequiredSetting<T>(SettingsRootNamespace settingsRootNamespace, string key)
    {
        if (SettingsReader.TryRead<T>(settingsRootNamespace, key, out var value))
        {
            return value;
        }

        throw new Exception($"Setting {key} of type {typeof(T)} is required");
    }
}
