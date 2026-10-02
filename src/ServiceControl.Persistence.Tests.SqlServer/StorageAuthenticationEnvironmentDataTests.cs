namespace ServiceControl.Persistence.Tests.SqlServer;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.SqlServer;

[TestFixture]
class StorageAuthenticationEnvironmentDataTests
{
    [TestCase("Server=.;Database=sc;Integrated Security=true", "Integrated")]
    [TestCase("Server=.;Database=sc;User Id=sc;Password=contoso-secret", "SqlPassword")]
    [TestCase("Server=tcp:contoso.database.windows.net;Database=sc;Authentication=Active Directory Managed Identity", "EntraId")]
    [TestCase("Server=tcp:contoso.database.windows.net;Database=sc;Authentication=Active Directory Default", "EntraId")]
    [TestCase("Server=tcp:contoso.database.windows.net;Database=sc;Authentication=Sql Password;User Id=sc;Password=contoso-secret", "SqlPassword")]
    public async Task Should_report_the_authentication_mode(string connectionString, string expected)
    {
        var settings = new SqlServerPersisterSettings { ConnectionString = connectionString, BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies" } };
        var datum = new SqlServerStorageAuthenticationEnvironmentDataProvider(settings).GetData().Single();

        Assert.That(await datum.ReadValue(CancellationToken.None), Is.EqualTo(expected));
    }
}
