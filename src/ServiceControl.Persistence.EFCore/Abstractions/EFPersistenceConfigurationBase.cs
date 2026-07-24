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
    const string AzureConnectionStringKey = "MessageBody/Azure/ConnectionString";
    const string AzureServiceUriKey = "MessageBody/Azure/ServiceUri";
    const string AzureManagedIdentityClientIdKey = "MessageBody/Azure/ManagedIdentityClientId";
    const string AzureAuthorityHostKey = "MessageBody/Azure/AuthorityHost";
    const string AzureContainerNameKey = "MessageBody/Azure/ContainerName";
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
        else if (settings.BodyStorageType == BodyStorageType.AzureBlob)
        {
            ConfigureAzureBlob(settings, settingsRootNamespace);
        }
    }

    static void ConfigureAzureBlob(EFPersisterSettings settings, SettingsRootNamespace settingsRootNamespace)
    {
        var connectionString = SettingsReader.Read<string>(settingsRootNamespace, AzureConnectionStringKey);
        var serviceUri = SettingsReader.Read<string>(settingsRootNamespace, AzureServiceUriKey);

        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasServiceUri = !string.IsNullOrWhiteSpace(serviceUri);

        // A connection string carries shared-key/SAS auth; a service URI uses managed identity. They
        // are mutually exclusive, and exactly one must be provided.
        if (hasConnectionString == hasServiceUri)
        {
            throw new Exception($"Azure Blob body storage requires exactly one of {AzureConnectionStringKey} (shared key / SAS) or {AzureServiceUriKey} (managed identity). {(hasConnectionString ? "Both were set." : "Neither was set.")}");
        }

        settings.AzureBlobConnectionString = hasConnectionString ? connectionString : null;
        settings.AzureBlobServiceUri = hasServiceUri ? serviceUri : null;
        settings.AzureBlobManagedIdentityClientId = SettingsReader.Read<string>(settingsRootNamespace, AzureManagedIdentityClientIdKey);
        settings.AzureBlobAuthorityHost = ReadAzureAuthorityHost(settingsRootNamespace);
        settings.AzureBlobContainerName = SettingsReader.Read(settingsRootNamespace, AzureContainerNameKey, settings.AzureBlobContainerName);
    }

    static string? ReadAzureAuthorityHost(SettingsRootNamespace settingsRootNamespace)
    {
        var raw = SettingsReader.Read<string>(settingsRootNamespace, AzureAuthorityHostKey);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out _))
        {
            throw new Exception($"Setting {AzureAuthorityHostKey} value '{raw}' is not a valid absolute URI.");
        }

        return raw;
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
