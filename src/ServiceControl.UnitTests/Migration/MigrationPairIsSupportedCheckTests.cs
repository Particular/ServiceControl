namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence;

[TestFixture]
[NonParallelizable]
class MigrationPairIsSupportedCheckTests
{
    [OneTimeSetUp]
    public static void TheTreeIsBuilt() =>
        Assert.That(
            PersistenceFactory.SqlPersistenceNames.Where(name => PersistenceManifestLibrary.Find(name) is null),
            Is.Empty,
            "These persistence manifests did not resolve, so this fixture cannot tell a supported pair from an unsupported one. Build the tree first with: dotnet build src --configuration Release -graph");

    [TearDown]
    public void ClearSourcePersistenceType() => Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_SOURCEPERSISTENCETYPE", null);

    [TestCase("SQLServer")]
    [TestCase("PostgreSQL")]
    public void RavenDB_to_a_SQL_persister_is_supported(string target) =>
        Assert.DoesNotThrowAsync(() => Check("RavenDB", target).Run());

    [TestCase("RavenDB", "RavenDB")]
    [TestCase("SQLServer", "PostgreSQL")]
    [TestCase("NotAPersister", "SQLServer")]
    public void Any_other_pair_is_refused_naming_both_settings(string source, string target)
    {
        var exception = Assert.ThrowsAsync<Exception>(() => Check(source, target).Run());

        Assert.That(exception.Message, Does.Contain("ServiceControl/Migration/SourcePersistenceType").And.Contain("ServiceControl/PersistenceType"));
    }

    static MigrationPairIsSupportedCheck Check(string source, string target)
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_SOURCEPERSISTENCETYPE", source);

        return new MigrationPairIsSupportedCheck(new Settings(transportType: "LearningTransport", persisterType: target, errorRetentionPeriod: TimeSpan.FromDays(10)));
    }
}
