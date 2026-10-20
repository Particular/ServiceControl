namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using Configuration;
    using Configuration.ServiceControl;

    // Names match ServiceControl's BodyStorageType so they are written to the config file as-is.
    public enum MessageBodyStorageType
    {
        FileSystem,
        AzureBlob,
        S3
    }

    public class MessageBodyStorage
    {
        public MessageBodyStorageType StorageType { get; set; }

        public string FileSystemStoragePath { get; set; }

        public string AzureConnectionString { get; set; }

        public string AzureServiceUri { get; set; }

        public string AzureManagedIdentityClientId { get; set; }

        public string AzureAuthorityHost { get; set; }

        public string AzureContainerName { get; set; }

        public string S3BucketName { get; set; }

        public string S3KeyPrefix { get; set; }

        public string S3Region { get; set; }

        public string S3ServiceUrl { get; set; }

        public string S3AccessKeyId { get; set; }

        public string S3SecretAccessKey { get; set; }

        internal IEnumerable<(SettingInfo Setting, string Value)> GetSettings()
        {
            yield return (ServiceControlSettings.MessageBodyStorageType, StorageType.ToString());

            switch (StorageType)
            {
                case MessageBodyStorageType.FileSystem:
                    yield return (ServiceControlSettings.MessageBodyFileSystemStoragePath, FileSystemStoragePath);
                    break;
                case MessageBodyStorageType.AzureBlob:
                    yield return (ServiceControlSettings.MessageBodyAzureConnectionString, AzureConnectionString);
                    yield return (ServiceControlSettings.MessageBodyAzureServiceUri, AzureServiceUri);
                    yield return (ServiceControlSettings.MessageBodyAzureManagedIdentityClientId, AzureManagedIdentityClientId);
                    yield return (ServiceControlSettings.MessageBodyAzureAuthorityHost, AzureAuthorityHost);
                    yield return (ServiceControlSettings.MessageBodyAzureContainerName, AzureContainerName);
                    break;
                case MessageBodyStorageType.S3:
                    yield return (ServiceControlSettings.MessageBodyS3BucketName, S3BucketName);
                    yield return (ServiceControlSettings.MessageBodyS3KeyPrefix, S3KeyPrefix);
                    yield return (ServiceControlSettings.MessageBodyS3Region, S3Region);
                    yield return (ServiceControlSettings.MessageBodyS3ServiceUrl, S3ServiceUrl);
                    yield return (ServiceControlSettings.MessageBodyS3AccessKeyId, S3AccessKeyId);
                    yield return (ServiceControlSettings.MessageBodyS3SecretAccessKey, S3SecretAccessKey);
                    break;
                default:
                    break;
            }
        }
    }
}
