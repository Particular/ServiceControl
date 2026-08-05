namespace ServiceControl.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

public abstract class EFPersistenceConfigurationBase : PersistenceConfiguration, IPersistenceConfiguration
{
    const string ConnectionStringKey = "Database/ConnectionString";
    const string CommandTimeoutKey = "Database/CommandTimeout";
    const string BodyStorageTypeKey = "MessageBody/StorageType";
    const string FileSystemStoragePathKey = "MessageBody/FileSystem/StoragePath";
    const string FileSystemDataSpaceRemainingThresholdKey = "MessageBody/FileSystem/DataSpaceRemainingThreshold";
    const string AzureConnectionStringKey = "MessageBody/Azure/ConnectionString";
    const string AzureServiceUriKey = "MessageBody/Azure/ServiceUri";
    const string AzureManagedIdentityClientIdKey = "MessageBody/Azure/ManagedIdentityClientId";
    const string AzureAuthorityHostKey = "MessageBody/Azure/AuthorityHost";
    const string AzureContainerNameKey = "MessageBody/Azure/ContainerName";
    const string S3BucketNameKey = "MessageBody/S3/BucketName";
    const string S3KeyPrefixKey = "MessageBody/S3/KeyPrefix";
    const string S3RegionKey = "MessageBody/S3/Region";
    const string S3ServiceUrlKey = "MessageBody/S3/ServiceUrl";
    const string S3AccessKeyIdKey = "MessageBody/S3/AccessKeyId";
    const string S3SecretAccessKeyKey = "MessageBody/S3/SecretAccessKey";
    const string MinBodySizeForCompressionKey = "MessageBody/MinCompressionSize";
    const string MaxBodySizeToStoreKey = "MaxBodySizeToStore";
    const string ErrorRetentionPeriodKey = "ErrorRetentionPeriod";
    const string EventsRetentionPeriodKey = "EventsRetentionPeriod";
    const string SubscriptionCacheDurationKey = "SubscriptionCacheDuration";

    public PersistenceSettings CreateSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var settings = CreateSettings(
            GetRequiredSetting<string>(settingsRootNamespace, ConnectionStringKey),
            CreateBodyStorageSettings(settingsRootNamespace));

        settings.CommandTimeout = SettingsReader.Read(settingsRootNamespace, CommandTimeoutKey, EFPersisterSettings.DefaultCommandTimeout);
        settings.ErrorRetentionPeriod = GetRequiredSetting<TimeSpan>(settingsRootNamespace, ErrorRetentionPeriodKey);
        settings.EventsRetentionPeriod = SettingsReader.Read(settingsRootNamespace, EventsRetentionPeriodKey, EFPersisterSettings.DefaultEventsRetentionPeriod);
        settings.SubscriptionCacheDuration = SettingsReader.Read(settingsRootNamespace, SubscriptionCacheDurationKey, EFPersisterSettings.DefaultSubscriptionCacheDuration);

        return settings;
    }

    public abstract IPersistence Create(PersistenceSettings settings);

    protected abstract EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage);

    static BodyStorageSettings CreateBodyStorageSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var storageType = ReadBodyStorageType(settingsRootNamespace);

        BodyStorageSettings bodyStorage = storageType switch
        {
            BodyStorageType.FileSystem => new FileSystemBodyStorageSettings
            {
                StoragePath = GetRequiredSetting<string>(settingsRootNamespace, FileSystemStoragePathKey),
                DataSpaceRemainingThreshold = ReadDataSpaceRemainingThreshold(settingsRootNamespace)
            },
            BodyStorageType.AzureBlob => CreateAzureBlobSettings(settingsRootNamespace),
            BodyStorageType.S3 => CreateS3Settings(settingsRootNamespace),
            _ => throw new ArgumentOutOfRangeException(nameof(settingsRootNamespace), storageType, "Unknown body storage type.")
        };

        bodyStorage.MinCompressionSize = SettingsReader.Read(settingsRootNamespace, MinBodySizeForCompressionKey, BodyStorageSettings.DefaultMinCompressionSize);
        bodyStorage.MaxBodySizeToStore = ReadMaxBodySizeToStore(settingsRootNamespace);

        return bodyStorage;
    }

    static S3BodyStorageSettings CreateS3Settings(SettingsRootNamespace settingsRootNamespace) =>
        new()
        {
            BucketName = GetRequiredSetting<string>(settingsRootNamespace, S3BucketNameKey),
            KeyPrefix = SettingsReader.Read(settingsRootNamespace, S3KeyPrefixKey, S3BodyStorageSettings.DefaultKeyPrefix),
            Region = SettingsReader.Read<string>(settingsRootNamespace, S3RegionKey),
            ServiceUrl = SettingsReader.Read<string>(settingsRootNamespace, S3ServiceUrlKey),
            Credentials = ReadS3Credentials(settingsRootNamespace)
        };

    static S3StaticCredentials? ReadS3Credentials(SettingsRootNamespace settingsRootNamespace)
    {
        var accessKeyId = SettingsReader.Read<string>(settingsRootNamespace, S3AccessKeyIdKey);
        var secretAccessKey = SettingsReader.Read<string>(settingsRootNamespace, S3SecretAccessKeyKey);
        var hasAccessKeyId = !string.IsNullOrWhiteSpace(accessKeyId);
        var hasSecretAccessKey = !string.IsNullOrWhiteSpace(secretAccessKey);

        if (hasAccessKeyId != hasSecretAccessKey)
        {
            throw new Exception($"S3 body storage requires both {S3AccessKeyIdKey} and {S3SecretAccessKeyKey} together, or neither to use the default AWS credential chain (IAM role).");
        }

        return hasAccessKeyId
            ? new S3StaticCredentials { AccessKeyId = accessKeyId, SecretAccessKey = secretAccessKey }
            : null;
    }

    static AzureBlobBodyStorageSettings CreateAzureBlobSettings(SettingsRootNamespace settingsRootNamespace) =>
        new()
        {
            Authentication = ReadAzureBlobAuthentication(settingsRootNamespace),
            ContainerName = SettingsReader.Read(settingsRootNamespace, AzureContainerNameKey, AzureBlobBodyStorageSettings.DefaultContainerName)
        };

    static AzureBlobAuthentication ReadAzureBlobAuthentication(SettingsRootNamespace settingsRootNamespace)
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

        if (hasConnectionString)
        {
            return new AzureBlobSharedKeyAuthentication { ConnectionString = connectionString };
        }

        return new AzureBlobManagedIdentityAuthentication
        {
            ServiceUri = ReadAbsoluteUri(serviceUri, AzureServiceUriKey),
            ClientId = SettingsReader.Read<string>(settingsRootNamespace, AzureManagedIdentityClientIdKey),
            AuthorityHost = ReadAzureAuthorityHost(settingsRootNamespace)
        };
    }

    static Uri? ReadAzureAuthorityHost(SettingsRootNamespace settingsRootNamespace)
    {
        var raw = SettingsReader.Read<string>(settingsRootNamespace, AzureAuthorityHostKey);

        return string.IsNullOrWhiteSpace(raw) ? null : ReadAbsoluteUri(raw, AzureAuthorityHostKey);
    }

    static Uri ReadAbsoluteUri(string raw, string key)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            throw new Exception($"Setting {key} value '{raw}' is not a valid absolute URI.");
        }

        return uri;
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

    static int ReadDataSpaceRemainingThreshold(SettingsRootNamespace settingsRootNamespace)
    {
        var threshold = SettingsReader.Read(settingsRootNamespace, FileSystemDataSpaceRemainingThresholdKey, FileSystemBodyStorageSettings.DefaultDataSpaceRemainingThreshold);

        if (threshold is < 0 or > 100)
        {
            throw new Exception($"Setting {FileSystemDataSpaceRemainingThresholdKey} value '{threshold}' is not valid. The value is a percentage between 0 and 100.");
        }

        return threshold;
    }

    static int ReadMaxBodySizeToStore(SettingsRootNamespace settingsRootNamespace)
    {
        var maxBodySizeToStore = SettingsReader.Read(settingsRootNamespace, MaxBodySizeToStoreKey, BodyStorageSettings.DefaultMaxBodySizeToStore);

        if (maxBodySizeToStore <= 0)
        {
            LoggerUtil.CreateStaticLogger<EFPersistenceConfigurationBase>()
                .LogError("MaxBodySizeToStore setting is invalid, 1 is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}", BodyStorageSettings.DefaultMaxBodySizeToStore);

            return BodyStorageSettings.DefaultMaxBodySizeToStore;
        }

        return maxBodySizeToStore;
    }
}
