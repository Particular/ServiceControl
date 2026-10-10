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
    public async Task Should_report_azure_blob_body_storage()
    {
        var data = await GetData(new AzureBlobBodyStorageSettings
        {
            Authentication = new AzureBlobManagedIdentityAuthentication { ServiceUri = new Uri("https://account.blob.core.windows.net") }
        });

        Assert.That(data["Storage.BodyStorage.Type"], Is.EqualTo("AzureBlob"));
    }

    [Test]
    public async Task Should_report_s3_body_storage()
    {
        var data = await GetData(new S3BodyStorageSettings { BucketName = "bodies" });

        Assert.That(data["Storage.BodyStorage.Type"], Is.EqualTo("S3"));
    }

    [Test]
    public async Task Should_report_file_system_body_storage()
    {
        var data = await GetData(new FileSystemBodyStorageSettings { StoragePath = "/var/lib/servicecontrol" });

        Assert.That(data["Storage.BodyStorage.Type"], Is.EqualTo("FileSystem"));
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
            Assert.That(data["Storage.Hosting"], Is.EqualTo("Unknown"));
            Assert.That(data["Storage.ServerVersion"], Is.EqualTo("Unknown"));
            Assert.That(data["Storage.HostingSource"], Is.EqualTo("None"));
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
