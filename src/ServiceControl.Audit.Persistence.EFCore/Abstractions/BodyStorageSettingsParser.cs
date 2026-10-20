namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence.BodyStorage;

// Mirrors the Error persister's MessageBody/* parsing, which reads a settings root rather than a dictionary.
static class BodyStorageSettingsParser
{
    internal const string StorageTypeKey = "MessageBody/StorageType";
    internal const string FileSystemStoragePathKey = "MessageBody/FileSystem/StoragePath";
    internal const string FileSystemDataSpaceRemainingThresholdKey = "MessageBody/FileSystem/DataSpaceRemainingThreshold";
    internal const string AzureConnectionStringKey = "MessageBody/Azure/ConnectionString";
    internal const string AzureServiceUriKey = "MessageBody/Azure/ServiceUri";
    internal const string AzureManagedIdentityClientIdKey = "MessageBody/Azure/ManagedIdentityClientId";
    internal const string AzureAuthorityHostKey = "MessageBody/Azure/AuthorityHost";
    internal const string AzureContainerNameKey = "MessageBody/Azure/ContainerName";
    internal const string S3BucketNameKey = "MessageBody/S3/BucketName";
    internal const string S3KeyPrefixKey = "MessageBody/S3/KeyPrefix";
    internal const string S3RegionKey = "MessageBody/S3/Region";
    internal const string S3ServiceUrlKey = "MessageBody/S3/ServiceUrl";
    internal const string S3AccessKeyIdKey = "MessageBody/S3/AccessKeyId";
    internal const string S3SecretAccessKeyKey = "MessageBody/S3/SecretAccessKey";
    internal const string MinCompressionSizeKey = "MessageBody/MinCompressionSize";

    // Audit instances must not share a location with Error or with each other, so the Audit defaults differ from the shared ones.
    internal const string DefaultAzureContainerName = "audit-bodies";
    internal const string DefaultS3KeyPrefix = "audit-bodies/";

    public static readonly string[] Keys =
    [
        StorageTypeKey,
        FileSystemStoragePathKey,
        FileSystemDataSpaceRemainingThresholdKey,
        AzureConnectionStringKey,
        AzureServiceUriKey,
        AzureManagedIdentityClientIdKey,
        AzureAuthorityHostKey,
        AzureContainerNameKey,
        S3BucketNameKey,
        S3KeyPrefixKey,
        S3RegionKey,
        S3ServiceUrlKey,
        S3AccessKeyIdKey,
        S3SecretAccessKeyKey,
        MinCompressionSizeKey
    ];

    public static BodyStorageSettings Parse(IDictionary<string, string> specific, int maxBodySizeToStore)
    {
        BodyStorageSettings bodyStorage = ReadStorageType(specific) switch
        {
            BodyStorageType.FileSystem => new FileSystemBodyStorageSettings
            {
                StoragePath = GetRequired(specific, FileSystemStoragePathKey),
                DataSpaceRemainingThreshold = ReadDataSpaceRemainingThreshold(specific)
            },
            BodyStorageType.AzureBlob => new AzureBlobBodyStorageSettings
            {
                Authentication = ReadAzureBlobAuthentication(specific),
                ContainerName = Get(specific, AzureContainerNameKey) ?? DefaultAzureContainerName
            },
            BodyStorageType.S3 => new S3BodyStorageSettings
            {
                BucketName = GetRequired(specific, S3BucketNameKey),
                KeyPrefix = Get(specific, S3KeyPrefixKey) ?? DefaultS3KeyPrefix,
                Region = Get(specific, S3RegionKey),
                ServiceUrl = Get(specific, S3ServiceUrlKey),
                Credentials = ReadS3Credentials(specific)
            },
            var unknown => throw new ArgumentOutOfRangeException(nameof(specific), unknown, "Unknown body storage type.")
        };

        bodyStorage.MinCompressionSize = ReadInt(specific, MinCompressionSizeKey, BodyStorageSettings.DefaultMinCompressionSize);
        bodyStorage.MaxBodySizeToStore = ReadMaxBodySizeToStore(maxBodySizeToStore);

        return bodyStorage;
    }

    static BodyStorageType ReadStorageType(IDictionary<string, string> specific)
    {
        var raw = Get(specific, StorageTypeKey);

        if (raw is null)
        {
            throw new InvalidOperationException($"{StorageTypeKey} is required. Valid values: {string.Join(", ", Enum.GetNames<BodyStorageType>())}.");
        }

        if (!Enum.TryParse<BodyStorageType>(raw, ignoreCase: true, out var value) || !Enum.IsDefined(value))
        {
            throw new InvalidOperationException($"{StorageTypeKey} value '{raw}' is not valid. Valid values: {string.Join(", ", Enum.GetNames<BodyStorageType>())}.");
        }

        return value;
    }

    static AzureBlobAuthentication ReadAzureBlobAuthentication(IDictionary<string, string> specific)
    {
        var connectionString = Get(specific, AzureConnectionStringKey);
        var serviceUri = Get(specific, AzureServiceUriKey);

        // A connection string carries shared-key/SAS auth; a service URI uses managed identity.
        if ((connectionString is null) == (serviceUri is null))
        {
            throw new InvalidOperationException($"Azure Blob body storage requires exactly one of {AzureConnectionStringKey} (shared key / SAS) or {AzureServiceUriKey} (managed identity). {(connectionString is null ? "Neither was set." : "Both were set.")}");
        }

        if (connectionString is not null)
        {
            return new AzureBlobSharedKeyAuthentication { ConnectionString = connectionString };
        }

        var authorityHost = Get(specific, AzureAuthorityHostKey);

        return new AzureBlobManagedIdentityAuthentication
        {
            ServiceUri = ReadAbsoluteUri(serviceUri!, AzureServiceUriKey),
            ClientId = Get(specific, AzureManagedIdentityClientIdKey),
            AuthorityHost = authorityHost is null ? null : ReadAbsoluteUri(authorityHost, AzureAuthorityHostKey)
        };
    }

    static S3StaticCredentials? ReadS3Credentials(IDictionary<string, string> specific)
    {
        var accessKeyId = Get(specific, S3AccessKeyIdKey);
        var secretAccessKey = Get(specific, S3SecretAccessKeyKey);

        if ((accessKeyId is null) != (secretAccessKey is null))
        {
            throw new InvalidOperationException($"S3 body storage requires both {S3AccessKeyIdKey} and {S3SecretAccessKeyKey} together, or neither to use the default AWS credential chain (IAM role).");
        }

        return accessKeyId is null ? null : new S3StaticCredentials { AccessKeyId = accessKeyId, SecretAccessKey = secretAccessKey! };
    }

    static Uri ReadAbsoluteUri(string raw, string key) =>
        Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"{key} value '{raw}' is not a valid absolute URI.");

    static int ReadDataSpaceRemainingThreshold(IDictionary<string, string> specific)
    {
        var threshold = ReadInt(specific, FileSystemDataSpaceRemainingThresholdKey, FileSystemBodyStorageSettings.DefaultDataSpaceRemainingThreshold);

        return threshold is < 0 or > 100
            ? throw new InvalidOperationException($"{FileSystemDataSpaceRemainingThresholdKey} value '{threshold}' is not valid. The value is a percentage between 0 and 100.")
            : threshold;
    }

    static int ReadMaxBodySizeToStore(int maxBodySizeToStore)
    {
        if (maxBodySizeToStore > 0)
        {
            return maxBodySizeToStore;
        }

        logger.LogError("MaxBodySizeToStore setting is invalid, 1 is the minimum value. Defaulting to {MaxBodySizeToStoreDefault}", BodyStorageSettings.DefaultMaxBodySizeToStore);

        return BodyStorageSettings.DefaultMaxBodySizeToStore;
    }

    static int ReadInt(IDictionary<string, string> specific, string key, int defaultValue) =>
        Get(specific, key) is not { } value ? defaultValue
        : int.TryParse(value, out var parsed) ? parsed
        : throw new InvalidOperationException($"{key} must be an integer.");

    static string GetRequired(IDictionary<string, string> specific, string key) =>
        Get(specific, key) ?? throw new InvalidOperationException($"{key} must be specified.");

    static string? Get(IDictionary<string, string> specific, string key) =>
        specific.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    static readonly ILogger logger = LoggerUtil.CreateStaticLogger<EFPersistenceConfigurationBase>();
}
