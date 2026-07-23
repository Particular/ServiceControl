namespace ServiceControl.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

public abstract class EFPersistenceConfigurationBase : IPersistenceConfiguration
{
    const string ConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string BodyStorageTypeKey = "MessageBody/StorageType";
    const string MessageBodyStoragePathKey = "MessageBody/StoragePath";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";
    const string MaxBodySizeToStoreKey = "MaxBodySizeToStore";
    const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
    const string EnableFullTextSearchOnBodiesKey = "EnableFullTextSearchOnBodies";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var settings = CreateSettings(GetRequiredSetting<string>(settingsRootNamespace, ConnectionStringKey));

        settings.CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, EFPersisterSettings.DefaultCommandTimeout);
        settings.MinBodySizeForCompression = SettingsReader.Read(settingsRootNamespace, MinBodySizeForCompressionKey, EFPersisterSettings.DefaultMinBodySizeForCompression);
        settings.MaxBodySizeToStore = ReadMaxBodySizeToStore(settingsRootNamespace);
        settings.ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey);
        settings.EnableFullTextSearchOnBodies = SettingsReader.Read(settingsRootNamespace, EnableFullTextSearchOnBodiesKey, true);

        ConfigureBodyStorage(settings, settingsRootNamespace);

        return settings;
    }

    public abstract IPersistence Create(PersistenceSettings settings);

    protected abstract EFPersisterSettings CreateSettings(string connectionString);

    static void ConfigureBodyStorage(EFPersisterSettings settings, SettingsRootNamespace settingsRootNamespace)
    {
        settings.BodyStorageType = ReadBodyStorageType(settingsRootNamespace);

        if (settings.BodyStorageType == BodyStorageType.FileSystem)
        {
            settings.MessageBodyStoragePath = GetRequiredSetting<string>(settingsRootNamespace, MessageBodyStoragePathKey);
        }
    }

    static BodyStorageType ReadBodyStorageType(SettingsRootNamespace settingsRootNamespace)
    {
        var raw = SettingsReader.Read<string>(settingsRootNamespace, BodyStorageTypeKey);

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new Exception($"Setting {BodyStorageTypeKey} is required. Valid values: {string.Join(", ", Enum.GetNames<BodyStorageType>())}.");
        }

        if (!Enum.TryParse<BodyStorageType>(raw, ignoreCase: true, out var value) || !Enum.IsDefined(value))
        {
            throw new Exception($"Setting {BodyStorageTypeKey} value '{raw}' is not valid. Valid values: {string.Join(", ", Enum.GetNames<BodyStorageType>())}.");
        }

        return value;
    }

    static int ReadMaxBodySizeToStore(SettingsRootNamespace settingsRootNamespace)
    {
        var maxBodySizeToStore = SettingsReader.Read(settingsRootNamespace, MaxBodySizeToStoreKey, EFPersisterSettings.DefaultMaxBodySizeToStore);

        if (maxBodySizeToStore <= 0)
        {
            LoggerUtil.CreateStaticLogger<EFPersistenceConfigurationBase>()
                .LogError("MaxBodySizeToStore setting is invalid, 1 is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}", EFPersisterSettings.DefaultMaxBodySizeToStore);

            return EFPersisterSettings.DefaultMaxBodySizeToStore;
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
