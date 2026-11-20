namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using Configuration;

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

        // settingsRoot is the instance's setting prefix, such as ServiceControl or ServiceControl.Audit.
        internal IEnumerable<(SettingInfo Setting, string Value)> GetSettings(string settingsRoot)
        {
            (SettingInfo, string) Setting(string name, string value) => (new SettingInfo { Name = $"{settingsRoot}/MessageBody/{name}" }, value);

            yield return Setting("StorageType", StorageType.ToString());

            switch (StorageType)
            {
                case MessageBodyStorageType.FileSystem:
                    yield return Setting("FileSystem/StoragePath", FileSystemStoragePath);
                    break;
                case MessageBodyStorageType.AzureBlob:
                    yield return Setting("Azure/ConnectionString", AzureConnectionString);
                    yield return Setting("Azure/ServiceUri", AzureServiceUri);
                    yield return Setting("Azure/ManagedIdentityClientId", AzureManagedIdentityClientId);
                    yield return Setting("Azure/AuthorityHost", AzureAuthorityHost);
                    yield return Setting("Azure/ContainerName", AzureContainerName);
                    break;
                case MessageBodyStorageType.S3:
                    yield return Setting("S3/BucketName", S3BucketName);
                    yield return Setting("S3/KeyPrefix", S3KeyPrefix);
                    yield return Setting("S3/Region", S3Region);
                    yield return Setting("S3/ServiceUrl", S3ServiceUrl);
                    yield return Setting("S3/AccessKeyId", S3AccessKeyId);
                    yield return Setting("S3/SecretAccessKey", S3SecretAccessKey);
                    break;
                default:
                    break;
            }
        }
    }
}
