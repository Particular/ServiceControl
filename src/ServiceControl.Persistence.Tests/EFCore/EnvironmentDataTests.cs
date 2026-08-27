namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation;
using ServiceControl.Persistence.EFCore.Infrastructure;

[TestFixture]
class DatabaseHostClassifierTests
{
    [TestCase("sc.database.windows.net", "AzureSql")]
    [TestCase("tcp:sc.database.windows.net,1433", "AzureSql")]
    [TestCase("SC.DATABASE.WINDOWS.NET", "AzureSql")]
    [TestCase("sc.postgres.database.azure.com", "AzurePostgres")]
    [TestCase("sc.abcdef.eu-west-1.rds.amazonaws.com", "AwsRds")]
    [TestCase("/cloudsql/my-project:europe-west1:sc", "GoogleCloudSql")]
    [TestCase("a.b.c.ravendb.cloud", "RavenCloud")]
    [TestCase("localhost", "SelfHosted")]
    [TestCase("127.0.0.1", "SelfHosted")]
    [TestCase("(local)", "SelfHosted")]
    [TestCase("(localdb)\\MSSQLLocalDB", "SelfHosted")]
    public void Should_classify_host(string host, string expected) =>
        Assert.That(DatabaseHostClassifier.Classify(host), Is.EqualTo(expected));

    [TestCase("sqlserver.internal.contoso.com")]
    [TestCase("db01.corp.example")]
    [TestCase("10.0.4.12")]
    public void Should_not_call_a_private_name_self_hosted(string host) =>
        Assert.That(DatabaseHostClassifier.Classify(host), Is.EqualTo("Unknown"),
            "A private DNS name in front of a managed database must not be counted as self-hosting");

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Should_report_unknown_when_there_is_no_host(string host) =>
        Assert.That(DatabaseHostClassifier.Classify(host), Is.EqualTo("Unknown"));
}

[TestFixture]
class EFEnvironmentDataProviderTests
{
    [Test]
    public async Task Should_report_managed_identity_for_azure_blob_service_uri()
    {
        var data = await GetData(new AzureBlobBodyStorageSettings
        {
            Authentication = new AzureBlobManagedIdentityAuthentication { ServiceUri = new Uri("https://account.blob.core.windows.net") }
        });

        Assert.Multiple(() =>
        {
            Assert.That(data["Persistence.BodyStorage.Type"], Is.EqualTo("AzureBlob"));
            Assert.That(data["Persistence.BodyStorage.Auth"], Is.EqualTo("ManagedIdentity"));
        });
    }

    [Test]
    public async Task Should_report_shared_key_for_azure_blob_connection_string()
    {
        var data = await GetData(new AzureBlobBodyStorageSettings
        {
            Authentication = new AzureBlobSharedKeyAuthentication { ConnectionString = "UseDevelopmentStorage=true" }
        });

        Assert.That(data["Persistence.BodyStorage.Auth"], Is.EqualTo("SharedKeyOrSas"));
    }

    [Test]
    public async Task Should_report_iam_role_when_s3_has_no_static_credentials()
    {
        var data = await GetData(new S3BodyStorageSettings { BucketName = "bodies" });

        Assert.Multiple(() =>
        {
            Assert.That(data["Persistence.BodyStorage.Type"], Is.EqualTo("S3"));
            Assert.That(data["Persistence.BodyStorage.Auth"], Is.EqualTo("IamRole"));
        });
    }

    [Test]
    public async Task Should_report_static_credentials_when_s3_has_an_access_key()
    {
        var data = await GetData(new S3BodyStorageSettings
        {
            BucketName = "bodies",
            Credentials = new S3StaticCredentials { AccessKeyId = "key", SecretAccessKey = "secret" }
        });

        Assert.That(data["Persistence.BodyStorage.Auth"], Is.EqualTo("StaticCredentials"));
    }

    [Test]
    public async Task Should_report_file_system_body_storage_as_not_applicable_for_auth()
    {
        var data = await GetData(new FileSystemBodyStorageSettings { StoragePath = "/var/lib/servicecontrol" });

        Assert.Multiple(() =>
        {
            Assert.That(data["Persistence.BodyStorage.Type"], Is.EqualTo("FileSystem"));
            Assert.That(data["Persistence.BodyStorage.Auth"], Is.EqualTo("NotApplicable"));
        });
    }

    [Test]
    public async Task Should_not_report_any_body_storage_secret_or_location()
    {
        var data = await GetData(new S3BodyStorageSettings
        {
            BucketName = "customer-bucket-name",
            Credentials = new S3StaticCredentials { AccessKeyId = "AKIAEXAMPLE", SecretAccessKey = "topsecret" }
        });

        foreach (var value in data.Values)
        {
            Assert.That(value, Does.Not.Contain("customer-bucket-name").And.Not.Contain("AKIAEXAMPLE").And.Not.Contain("topsecret"));
        }
    }

    [Test]
    public async Task Should_fall_back_to_unknown_when_the_hosting_probe_fails()
    {
        var data = await GetData(new FileSystemBodyStorageSettings { StoragePath = "/var/lib/servicecontrol" }, new FailingHostingProbe());

        Assert.Multiple(() =>
        {
            Assert.That(data["Persistence.Hosting"], Is.EqualTo("Unknown"));
            Assert.That(data["Persistence.ServerVersion"], Is.EqualTo("Unknown"));
            Assert.That(data["Persistence.HostingSource"], Is.EqualTo("None"));
        });
    }

    static async Task<Dictionary<string, string>> GetData(BodyStorageSettings bodyStorage, IDatabaseHostingProbe hostingProbe = null)
    {
        var settings = new TestPersisterSettings { ConnectionString = "Host=localhost", BodyStorage = bodyStorage };
        var provider = new EFEnvironmentDataProvider(settings, hostingProbe ?? new TestHostingProbe());
        var data = new Dictionary<string, string>();

        foreach (var datum in provider.GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }

        return data;
    }

    class TestPersisterSettings : EFPersisterSettings;

    class TestHostingProbe : IDatabaseHostingProbe
    {
        public string StorageName => "PostgreSQL";

        public Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DatabaseHosting("SelfHosted", "17", DatabaseHostingSource.Probe));
    }

    class FailingHostingProbe : IDatabaseHostingProbe
    {
        public string StorageName => "PostgreSQL";

        public Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHosting.Unclassified);
    }
}
