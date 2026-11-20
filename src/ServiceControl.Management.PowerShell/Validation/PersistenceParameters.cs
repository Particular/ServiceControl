namespace ServiceControl.Management.PowerShell.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    // Checks that only the parameters the selected storage type uses are given, like the transport connection string check.
    static class PersistenceParameters
    {
        public static void Validate(PersistenceManifest manifest, string dbPath, int? databaseMaintenancePort, string databaseConnectionString, bool enableFullTextSearchOnBodiesSpecified)
        {
            Check(manifest, "DBPath", manifest.Settings.Any(s => s.Name.EndsWith("/DBPath")), !string.IsNullOrWhiteSpace(dbPath));
            Check(manifest, "DatabaseMaintenancePort", manifest.Settings.Any(s => s.Name.EndsWith("/DatabaseMaintenancePort")), databaseMaintenancePort.HasValue);
            Check(manifest, "DatabaseConnectionString", manifest.Settings.Any(s => s.Mandatory && s.Name.EndsWith("/Database/ConnectionString")), !string.IsNullOrWhiteSpace(databaseConnectionString));

            if (enableFullTextSearchOnBodiesSpecified && !manifest.Settings.Any(s => s.Name.EndsWith("/EnableFullTextSearchOnBodies")))
            {
                throw new Exception($"The '{manifest.Name}' storage type does not use EnableFullTextSearchOnBodies.");
            }
        }

        // Returns null for storage types that keep message bodies in the database.
        public static MessageBodyStorage CreateMessageBodyStorage(PersistenceManifest manifest, string storageTypeName, IReadOnlyDictionary<string, string> parameters)
        {
            var supportsBodyStorage = manifest.Settings.Any(s => s.Name.EndsWith("/MessageBody/StorageType"));
            var specified = parameters.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToList();

            if (!supportsBodyStorage)
            {
                if (storageTypeName != null)
                {
                    specified.Insert(0, "MessageBodyStorageType");
                }

                if (specified.Count > 0)
                {
                    throw new Exception($"The '{manifest.Name}' storage type does not use {string.Join(", ", specified)}.");
                }

                return null;
            }

            var storageType = storageTypeName == null ? MessageBodyStorageType.FileSystem : Enum.Parse<MessageBodyStorageType>(storageTypeName);
            var parameterPrefix = storageType switch
            {
                MessageBodyStorageType.FileSystem => "MessageBodyPath",
                MessageBodyStorageType.AzureBlob => "AzureBlob",
                MessageBodyStorageType.S3 => "S3",
                _ => throw new Exception($"Message body storage type '{storageType}' is not supported.")
            };

            var unused = specified.Where(name => !name.StartsWith(parameterPrefix)).ToList();
            if (unused.Count > 0)
            {
                throw new Exception($"The '{storageType}' message body storage type does not use {string.Join(", ", unused)}.");
            }

            string Value(string name) => parameters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            var requiredParameter = storageType switch
            {
                MessageBodyStorageType.FileSystem => "MessageBodyPath",
                MessageBodyStorageType.S3 => "S3BucketName",
                // Azure Blob needs one of two parameters, which the installer checks.
                MessageBodyStorageType.AzureBlob => null,
                _ => null
            };

            if (requiredParameter != null && Value(requiredParameter) == null)
            {
                throw new Exception($"{requiredParameter} is mandatory for the '{storageType}' message body storage type.");
            }

            return new MessageBodyStorage
            {
                StorageType = storageType,
                FileSystemStoragePath = Value("MessageBodyPath"),
                AzureConnectionString = Value("AzureBlobConnectionString"),
                AzureServiceUri = Value("AzureBlobServiceUri"),
                AzureManagedIdentityClientId = Value("AzureBlobManagedIdentityClientId"),
                AzureAuthorityHost = Value("AzureBlobAuthorityHost"),
                AzureContainerName = Value("AzureBlobContainerName"),
                S3BucketName = Value("S3BucketName"),
                S3KeyPrefix = Value("S3KeyPrefix"),
                S3Region = Value("S3Region"),
                S3ServiceUrl = Value("S3ServiceUrl"),
                S3AccessKeyId = Value("S3AccessKeyId"),
                S3SecretAccessKey = Value("S3SecretAccessKey")
            };
        }

        static void Check(PersistenceManifest manifest, string parameterName, bool isUsed, bool isSpecified)
        {
            if (isUsed && !isSpecified)
            {
                throw new Exception($"{parameterName} is mandatory for the '{manifest.Name}' storage type.");
            }

            if (!isUsed && isSpecified)
            {
                throw new Exception($"The '{manifest.Name}' storage type does not use {parameterName}.");
            }
        }
    }
}
