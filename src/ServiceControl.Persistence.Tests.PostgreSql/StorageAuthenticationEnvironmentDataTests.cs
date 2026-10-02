namespace ServiceControl.Persistence.Tests.PostgreSql;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.PostgreSql;

[TestFixture]
class StorageAuthenticationEnvironmentDataTests
{
    [TestCase("Host=localhost;Database=sc;Username=sc;Password=contoso-secret", "Password")]
    [TestCase("Host=localhost;Database=sc;Username=sc;Passfile=/home/sc/.pgpass", "Password")]
    [TestCase("Host=localhost;Database=sc;Username=sc;SSL Certificate=/certs/client.crt", "ClientCertificate")]
    [TestCase("Host=localhost;Database=sc;Username=sc", "None")]
    public async Task Should_report_the_authentication_mode(string connectionString, string expected)
    {
        var settings = new PostgreSqlPersisterSettings { ConnectionString = connectionString, BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies" } };
        var datum = new PostgreSqlStorageAuthenticationEnvironmentDataProvider(settings).GetData().Single();

        Assert.That(await datum.ReadValue(CancellationToken.None), Is.EqualTo(expected));
    }
}
