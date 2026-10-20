namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Persistence.BodyStorage;

    [TestFixture]
    class BodyStorageSettingsParserTests
    {
        const int MaxBodySizeToStore = 1234;

        static Dictionary<string, string> Settings(params (string Key, string Value)[] values)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values)
            {
                settings[key] = value;
            }

            return settings;
        }

        static BodyStorageSettings Parse(params (string Key, string Value)[] values) =>
            BodyStorageSettingsParser.Parse(Settings(values), MaxBodySizeToStore);

        [Test]
        public void The_storage_type_is_required()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => Parse());

            Assert.That(exception.Message, Does.Contain("MessageBody/StorageType"));
        }

        [Test]
        public void An_unknown_storage_type_is_rejected() =>
            Assert.Throws<InvalidOperationException>(() => Parse(("MessageBody/StorageType", "Tape")));

        [Test]
        public void File_system_settings_use_the_defaults()
        {
            var settings = (FileSystemBodyStorageSettings)Parse(
                ("MessageBody/StorageType", "filesystem"),
                ("MessageBody/FileSystem/StoragePath", "/bodies"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.StoragePath, Is.EqualTo("/bodies"));
                Assert.That(settings.DataSpaceRemainingThreshold, Is.EqualTo(FileSystemBodyStorageSettings.DefaultDataSpaceRemainingThreshold));
                Assert.That(settings.MinCompressionSize, Is.EqualTo(BodyStorageSettings.DefaultMinCompressionSize));
                Assert.That(settings.MaxBodySizeToStore, Is.EqualTo(MaxBodySizeToStore));
            }
        }

        [Test]
        public void The_file_system_path_is_required() =>
            Assert.Throws<InvalidOperationException>(() => Parse(("MessageBody/StorageType", "FileSystem")));

        [TestCase("-1")]
        [TestCase("101")]
        [TestCase("lots")]
        public void An_invalid_file_system_threshold_is_rejected(string threshold) =>
            Assert.Throws<InvalidOperationException>(() => Parse(
                ("MessageBody/StorageType", "FileSystem"),
                ("MessageBody/FileSystem/StoragePath", "/bodies"),
                ("MessageBody/FileSystem/DataSpaceRemainingThreshold", threshold)));

        [Test]
        public void The_compression_threshold_can_be_set()
        {
            var settings = Parse(
                ("MessageBody/StorageType", "FileSystem"),
                ("MessageBody/FileSystem/StoragePath", "/bodies"),
                ("MessageBody/MinCompressionSize", "10"));

            Assert.That(settings.MinCompressionSize, Is.EqualTo(10));
        }

        [Test]
        public void An_invalid_max_body_size_falls_back_to_the_default()
        {
            var settings = BodyStorageSettingsParser.Parse(
                Settings(("MessageBody/StorageType", "FileSystem"), ("MessageBody/FileSystem/StoragePath", "/bodies")),
                0);

            Assert.That(settings.MaxBodySizeToStore, Is.EqualTo(BodyStorageSettings.DefaultMaxBodySizeToStore));
        }

        [Test]
        public void Azure_shared_key_uses_the_audit_container_by_default()
        {
            var settings = (AzureBlobBodyStorageSettings)Parse(
                ("MessageBody/StorageType", "AzureBlob"),
                ("MessageBody/Azure/ConnectionString", "UseDevelopmentStorage=true"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.Authentication, Is.TypeOf<AzureBlobSharedKeyAuthentication>());
                Assert.That(settings.ContainerName, Is.EqualTo("audit-bodies"));
            }
        }

        [Test]
        public void Azure_managed_identity_reads_its_options()
        {
            var settings = (AzureBlobBodyStorageSettings)Parse(
                ("MessageBody/StorageType", "AzureBlob"),
                ("MessageBody/Azure/ServiceUri", "https://account.blob.core.windows.net"),
                ("MessageBody/Azure/ManagedIdentityClientId", "client"),
                ("MessageBody/Azure/ContainerName", "custom"));

            var authentication = (AzureBlobManagedIdentityAuthentication)settings.Authentication;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(authentication.ServiceUri, Is.EqualTo(new Uri("https://account.blob.core.windows.net")));
                Assert.That(authentication.ClientId, Is.EqualTo("client"));
                Assert.That(settings.ContainerName, Is.EqualTo("custom"));
            }
        }

        [Test]
        public void Azure_requires_exactly_one_authentication_mode()
        {
            Assert.Throws<InvalidOperationException>(() => Parse(("MessageBody/StorageType", "AzureBlob")));
            Assert.Throws<InvalidOperationException>(() => Parse(
                ("MessageBody/StorageType", "AzureBlob"),
                ("MessageBody/Azure/ConnectionString", "UseDevelopmentStorage=true"),
                ("MessageBody/Azure/ServiceUri", "https://account.blob.core.windows.net")));
        }

        [Test]
        public void Azure_rejects_a_relative_service_uri() =>
            Assert.Throws<InvalidOperationException>(() => Parse(
                ("MessageBody/StorageType", "AzureBlob"),
                ("MessageBody/Azure/ServiceUri", "account")));

        [Test]
        public void S3_uses_the_audit_prefix_by_default()
        {
            var settings = (S3BodyStorageSettings)Parse(
                ("MessageBody/StorageType", "S3"),
                ("MessageBody/S3/BucketName", "bucket"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.BucketName, Is.EqualTo("bucket"));
                Assert.That(settings.KeyPrefix, Is.EqualTo("audit-bodies/"));
                Assert.That(settings.Credentials, Is.Null);
            }
        }

        [Test]
        public void S3_requires_a_bucket() =>
            Assert.Throws<InvalidOperationException>(() => Parse(("MessageBody/StorageType", "S3")));

        [Test]
        public void S3_credentials_must_come_as_a_pair()
        {
            Assert.Throws<InvalidOperationException>(() => Parse(
                ("MessageBody/StorageType", "S3"),
                ("MessageBody/S3/BucketName", "bucket"),
                ("MessageBody/S3/AccessKeyId", "key")));

            var settings = (S3BodyStorageSettings)Parse(
                ("MessageBody/StorageType", "S3"),
                ("MessageBody/S3/BucketName", "bucket"),
                ("MessageBody/S3/AccessKeyId", "key"),
                ("MessageBody/S3/SecretAccessKey", "secret"));

            Assert.That(settings.Credentials.AccessKeyId, Is.EqualTo("key"));
        }

        [Test]
        public void Every_key_is_read_from_the_configuration()
        {
            Assert.That(new TestConfiguration().ConfigurationKeys, Is.SupersetOf(BodyStorageSettingsParser.Keys));
        }

        sealed class TestConfiguration : EFPersistenceConfigurationBase
        {
            public override string Name => "Test";

            protected override IPersistence Create(EFPersisterSettings settings) => throw new NotSupportedException();
        }
    }
}
