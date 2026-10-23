namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using Engine.Validation;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class MessageBodyStorageValidationTests
    {
        static PersistenceManifest SqlServer => ServiceControlPersisters.GetPrimaryPersistence("SQLServer");
        static PersistenceManifest RavenDB => ServiceControlPersisters.GetPrimaryPersistence("RavenDB");

        [Test]
        public void Should_reject_body_storage_for_RavenDB()
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.FileSystem, FileSystemStoragePath = @"C:\bodies" };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(RavenDB, storage));
        }

        [Test]
        public void Should_allow_no_body_storage_for_RavenDB() =>
            Assert.DoesNotThrow(() => MessageBodyStorageValidator.Validate(RavenDB, null));

        [Test]
        public void Should_require_body_storage_for_SQL_Server() =>
            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, null));

        [Test]
        public void Should_require_path_for_file_system()
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.FileSystem };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [TestCase(null, null)]
        [TestCase("UseDevelopmentStorage=true", "https://account.blob.core.windows.net")]
        public void Should_require_exactly_one_Azure_authentication_method(string connectionString, string serviceUri)
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.AzureBlob, AzureConnectionString = connectionString, AzureServiceUri = serviceUri };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [Test]
        public void Should_reject_relative_Azure_service_uri()
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.AzureBlob, AzureServiceUri = "account.blob.core.windows.net" };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [TestCase("UseDevelopmentStorage=true", null)]
        [TestCase(null, "https://account.blob.core.windows.net")]
        public void Should_accept_one_Azure_authentication_method(string connectionString, string serviceUri)
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.AzureBlob, AzureConnectionString = connectionString, AzureServiceUri = serviceUri };

            Assert.DoesNotThrow(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [Test]
        public void Should_require_S3_bucket()
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.S3 };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [TestCase("key", null)]
        [TestCase(null, "secret")]
        public void Should_require_both_S3_keys_or_neither(string accessKeyId, string secretAccessKey)
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.S3, S3BucketName = "bodies", S3AccessKeyId = accessKeyId, S3SecretAccessKey = secretAccessKey };

            Assert.Throws<EngineValidationException>(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }

        [TestCase(null, null)]
        [TestCase("key", "secret")]
        public void Should_accept_S3_with_both_keys_or_neither(string accessKeyId, string secretAccessKey)
        {
            var storage = new MessageBodyStorage { StorageType = MessageBodyStorageType.S3, S3BucketName = "bodies", S3AccessKeyId = accessKeyId, S3SecretAccessKey = secretAccessKey };

            Assert.DoesNotThrow(() => MessageBodyStorageValidator.Validate(SqlServer, storage));
        }
    }
}
