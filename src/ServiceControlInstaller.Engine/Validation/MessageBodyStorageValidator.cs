namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Linq;
    using Configuration.ServiceControl;
    using Instances;

    // Mirrors the checks ServiceControl runs at startup, so a bad combination fails at install time instead.
    static class MessageBodyStorageValidator
    {
        public static void Validate(PersistenceManifest manifest, MessageBodyStorage storage)
        {
            var supportsBodyStorage = manifest.Settings.Any(s => s.Name == ServiceControlSettings.MessageBodyStorageType.Name);

            if (!supportsBodyStorage)
            {
                if (storage != null)
                {
                    throw new EngineValidationException($"The {manifest.DisplayName} storage type does not support configuring message body storage.");
                }

                return;
            }

            if (storage == null)
            {
                throw new EngineValidationException($"Message body storage must be configured for the {manifest.DisplayName} storage type.");
            }

            switch (storage.StorageType)
            {
                case MessageBodyStorageType.FileSystem:
                    if (IsEmpty(storage.FileSystemStoragePath))
                    {
                        throw new EngineValidationException("A message body path is required when message bodies are stored on the file system.");
                    }

                    break;
                case MessageBodyStorageType.AzureBlob:
                    var hasConnectionString = !IsEmpty(storage.AzureConnectionString);
                    var hasServiceUri = !IsEmpty(storage.AzureServiceUri);

                    if (hasConnectionString == hasServiceUri)
                    {
                        throw new EngineValidationException("Azure Blob Storage requires either a connection string or a service URI for managed identity, but not both.");
                    }

                    CheckAbsoluteUri(storage.AzureServiceUri, "Azure Blob Storage service URI");
                    CheckAbsoluteUri(storage.AzureAuthorityHost, "Azure authority host");
                    break;
                case MessageBodyStorageType.S3:
                    if (IsEmpty(storage.S3BucketName))
                    {
                        throw new EngineValidationException("An S3 bucket name is required when message bodies are stored in Amazon S3.");
                    }

                    if (IsEmpty(storage.S3AccessKeyId) != IsEmpty(storage.S3SecretAccessKey))
                    {
                        throw new EngineValidationException("Specify both the S3 access key ID and secret access key, or neither to use the default AWS credentials.");
                    }

                    CheckAbsoluteUri(storage.S3ServiceUrl, "S3 service URL");
                    break;
                default:
                    throw new EngineValidationException($"Message body storage type '{storage.StorageType}' is not supported.");
            }
        }

        static void CheckAbsoluteUri(string value, string name)
        {
            if (!IsEmpty(value) && !Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                throw new EngineValidationException($"The {name} '{value}' is not a valid absolute URI.");
            }
        }

        static bool IsEmpty(string value) => string.IsNullOrWhiteSpace(value);
    }
}
