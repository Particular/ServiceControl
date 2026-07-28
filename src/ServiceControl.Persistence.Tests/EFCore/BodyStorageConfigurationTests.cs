namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.EFCore.Abstractions;

// Covers the settings-reader validation that lets the storage settings expose non-nullable members,
// so the stores never have to re-check what configuration already guaranteed.
[TestFixture]
[NonParallelizable]
class BodyStorageConfigurationTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    static readonly string[] Keys =
    [
        "SERVICECONTROL_DATABASE_CONNECTIONSTRING",
        "SERVICECONTROL_ERRORRETENTIONPERIOD",
        "SERVICECONTROL_MESSAGEBODY_STORAGETYPE",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_DATASPACEREMAININGTHRESHOLD",
        "SERVICECONTROL_MESSAGEBODY_MINCOMPRESSIONSIZE",
        "SERVICECONTROL_MESSAGEBODY_AZURE_CONNECTIONSTRING",
        "SERVICECONTROL_MESSAGEBODY_AZURE_SERVICEURI",
        "SERVICECONTROL_MESSAGEBODY_AZURE_MANAGEDIDENTITYCLIENTID",
        "SERVICECONTROL_MESSAGEBODY_AZURE_AUTHORITYHOST",
        "SERVICECONTROL_MESSAGEBODY_AZURE_CONTAINERNAME",
        "SERVICECONTROL_MESSAGEBODY_S3_BUCKETNAME",
        "SERVICECONTROL_MESSAGEBODY_S3_KEYPREFIX",
        "SERVICECONTROL_MESSAGEBODY_S3_REGION",
        "SERVICECONTROL_MESSAGEBODY_S3_ACCESSKEYID",
        "SERVICECONTROL_MESSAGEBODY_S3_SECRETACCESSKEY"
    ];

    [SetUp]
    public void SetUp()
    {
        ClearKeys();
        Set("SERVICECONTROL_DATABASE_CONNECTIONSTRING", "Server=nowhere");
        Set("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
    }

    [TearDown]
    public void TearDown() => ClearKeys();

    [Test]
    public void File_system_storage_yields_a_populated_path()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Set("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");

        var bodyStorage = CreateBodyStorageSettings();

        Assert.That(bodyStorage, Is.TypeOf<FileSystemBodyStorageSettings>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((FileSystemBodyStorageSettings)bodyStorage).StoragePath, Is.EqualTo("/var/bodies"));
            Assert.That(((FileSystemBodyStorageSettings)bodyStorage).DataSpaceRemainingThreshold, Is.EqualTo(FileSystemBodyStorageSettings.DefaultDataSpaceRemainingThreshold));
        }
    }

    [Test]
    public void File_system_storage_reads_the_data_space_remaining_threshold()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Set("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");
        Set("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_DATASPACEREMAININGTHRESHOLD", "30");

        var bodyStorage = (FileSystemBodyStorageSettings)CreateBodyStorageSettings();

        Assert.That(bodyStorage.DataSpaceRemainingThreshold, Is.EqualTo(30));
    }

    [TestCase("-1")]
    [TestCase("101")]
    public void A_data_space_remaining_threshold_outside_the_percentage_range_is_rejected(string threshold)
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Set("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");
        Set("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_DATASPACEREMAININGTHRESHOLD", threshold);

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("MessageBody/FileSystem/DataSpaceRemainingThreshold"));
    }

    [Test]
    public void Azure_connection_string_yields_shared_key_authentication()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_CONNECTIONSTRING", "UseDevelopmentStorage=true");

        var azure = (AzureBlobBodyStorageSettings)CreateBodyStorageSettings();

        Assert.That(azure.Authentication, Is.TypeOf<AzureBlobSharedKeyAuthentication>());
        Assert.That(((AzureBlobSharedKeyAuthentication)azure.Authentication).ConnectionString, Is.EqualTo("UseDevelopmentStorage=true"));
    }

    [Test]
    public void Azure_service_uri_yields_managed_identity_authentication()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_SERVICEURI", "https://account.blob.core.windows.net");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_MANAGEDIDENTITYCLIENTID", "client-id");

        var azure = (AzureBlobBodyStorageSettings)CreateBodyStorageSettings();

        var managedIdentity = azure.Authentication as AzureBlobManagedIdentityAuthentication;
        Assert.That(managedIdentity, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(managedIdentity.ServiceUri, Is.EqualTo(new Uri("https://account.blob.core.windows.net")));
            Assert.That(managedIdentity.ClientId, Is.EqualTo("client-id"));
            Assert.That(managedIdentity.AuthorityHost, Is.Null);
        }
    }

    [Test]
    public void S3_without_keys_falls_back_to_the_default_credential_chain()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "S3");
        Set("SERVICECONTROL_MESSAGEBODY_S3_BUCKETNAME", "bodies");

        var s3 = (S3BodyStorageSettings)CreateBodyStorageSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(s3.BucketName, Is.EqualTo("bodies"));
            Assert.That(s3.Credentials, Is.Null);
        }
    }

    [Test]
    public void S3_with_both_keys_yields_static_credentials()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "S3");
        Set("SERVICECONTROL_MESSAGEBODY_S3_BUCKETNAME", "bodies");
        Set("SERVICECONTROL_MESSAGEBODY_S3_ACCESSKEYID", "key");
        Set("SERVICECONTROL_MESSAGEBODY_S3_SECRETACCESSKEY", "secret");

        var s3 = (S3BodyStorageSettings)CreateBodyStorageSettings();

        Assert.That(s3.Credentials, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(s3.Credentials.AccessKeyId, Is.EqualTo("key"));
            Assert.That(s3.Credentials.SecretAccessKey, Is.EqualTo("secret"));
        }
    }

    [Test]
    public void Missing_storage_type_is_rejected() =>
        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("MessageBody/StorageType"));

    [Test]
    public void Unknown_storage_type_is_rejected()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "Dropbox");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("is not valid"));
    }

    [Test]
    public void File_system_storage_without_a_path_is_rejected()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("MessageBody/FileSystem/StoragePath"));
    }

    [Test]
    public void S3_storage_without_a_bucket_is_rejected()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "S3");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("MessageBody/S3/BucketName"));
    }

    [TestCase("SERVICECONTROL_MESSAGEBODY_S3_ACCESSKEYID")]
    [TestCase("SERVICECONTROL_MESSAGEBODY_S3_SECRETACCESSKEY")]
    public void A_lone_S3_credential_half_is_rejected(string key)
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "S3");
        Set("SERVICECONTROL_MESSAGEBODY_S3_BUCKETNAME", "bodies");
        Set(key, "value");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("together"));
    }

    [Test]
    public void Azure_storage_without_any_credential_is_rejected()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("Neither was set."));
    }

    [Test]
    public void Azure_storage_with_both_credentials_is_rejected()
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_CONNECTIONSTRING", "UseDevelopmentStorage=true");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_SERVICEURI", "https://account.blob.core.windows.net");

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("Both were set."));
    }

    [TestCase("SERVICECONTROL_MESSAGEBODY_AZURE_SERVICEURI", "not-a-uri")]
    [TestCase("SERVICECONTROL_MESSAGEBODY_AZURE_AUTHORITYHOST", "not-a-uri")]
    public void A_non_absolute_Azure_uri_is_rejected(string key, string value)
    {
        Set("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");
        Set("SERVICECONTROL_MESSAGEBODY_AZURE_SERVICEURI", "https://account.blob.core.windows.net");
        Set(key, value);

        Assert.That(CreateBodyStorageSettings, Throws.Exception.With.Message.Contains("is not a valid absolute URI"));
    }

    static BodyStorageSettings CreateBodyStorageSettings() =>
        ((EFPersisterSettings)new TestPersistenceConfiguration().CreateSettings(TestNamespace)).BodyStorage;

    static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);

    static void ClearKeys()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    sealed class TestPersistenceConfiguration : EFPersistenceConfigurationBase
    {
        public override IPersistence Create(PersistenceSettings settings) => throw new NotSupportedException();

        protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
            new TestPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
    }

    sealed class TestPersisterSettings : EFPersisterSettings;
}
