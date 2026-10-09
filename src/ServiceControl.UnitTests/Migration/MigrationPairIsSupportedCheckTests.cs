namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence;

[TestFixture]
class MigrationPairIsSupportedCheckTests
{
    [OneTimeSetUp]
    public static void TheTreeIsBuilt() =>
        Assert.That(
            PersistenceFactory.SqlPersistenceNames.Where(name => PersistenceManifestLibrary.Find(name) is null),
            Is.Empty,
            "These persistence manifests did not resolve, so this fixture cannot tell a supported pair from an unsupported one. Build the tree first with: dotnet build src --configuration Release -graph");

    [TestCase("SQLServer")]
    [TestCase("PostgreSQL")]
    public void RavenDB_to_a_SQL_persister_is_supported(string target) =>
        Assert.DoesNotThrowAsync(() => Check(target).Run());

    [Test]
    public void A_RavenDB_target_is_refused_naming_the_target_setting()
    {
        var exception = Assert.ThrowsAsync<Exception>(() => Check("RavenDB").Run());

        Assert.That(exception.Message, Does.Contain("ServiceControl/PersistenceType"));
    }

    static MigrationPairIsSupportedCheck Check(string target) =>
        new(new Settings(transportType: "LearningTransport", persisterType: target, errorRetentionPeriod: TimeSpan.FromDays(10)));
}
