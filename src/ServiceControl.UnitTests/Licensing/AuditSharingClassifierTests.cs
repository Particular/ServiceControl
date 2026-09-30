namespace ServiceControl.UnitTests.Licensing;

using NUnit.Framework;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Api;
using ServiceControl.Persistence;

[TestFixture]
public class AuditSharingClassifierTests
{
    [TestCase(null, "a", "Unknown")]
    [TestCase("a", null, "Unknown")]
    [TestCase("NotApplicable", "a", "NotApplicable")]
    [TestCase("a", "NotApplicable", "NotApplicable")]
    [TestCase("a", "a", "True")]
    [TestCase("a", "b", "False")]
    public void Same_machine(string local, string remote, string expected) =>
        Assert.That(AuditSharingClassifier.SameMachine(local, remote), Is.EqualTo(expected));

    [Test]
    public void Sharing_is_unknown_without_both_identities()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AuditSharingClassifier.DatabaseSharing(null, Identity()), Is.EqualTo("Unknown"));
            Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(), null), Is.EqualTo("Unknown"));
        }
    }

    [Test]
    public void Different_engines_cannot_share() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(engine: "PostgreSQL"), Identity(engine: "RavenDB")), Is.EqualTo("NotApplicable"));

    [Test]
    public void Engine_comparison_ignores_case() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(engine: "SQLServer"), Identity(engine: "sqlserver")), Is.EqualTo("SameSchema"));

    [Test]
    public void Different_servers_are_separate() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(), Identity(server: "other")), Is.EqualTo("SeparateServer"));

    [Test]
    public void Different_databases_on_one_server_share_the_server() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(), Identity(database: "other")), Is.EqualTo("SameServer"));

    [Test]
    public void Equal_schemas_share_the_schema() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(), Identity()), Is.EqualTo("SameSchema"));

    [Test]
    public void Different_schemas_share_the_database() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(), Identity(schema: "other")), Is.EqualTo("SameDatabase"));

    [Test]
    public void Schemaless_engines_share_the_database_at_most() =>
        Assert.That(AuditSharingClassifier.DatabaseSharing(Identity(engine: "RavenDB", schema: null), Identity(engine: "RavenDB", schema: null)), Is.EqualTo("SameDatabase"));

    [Test]
    public void Hashing_normalises_case_and_whitespace()
    {
        var hashed = AuditSharingClassifier.Hash(new StorageIdentity("SQLServer", " PRODSQL01 ", "ServiceControl", "dbo"));

        Assert.That(hashed.ServerHash, Is.EqualTo(IdentityHash.Compute("prodsql01")));
    }

    [Test]
    public void Hashing_nothing_is_nothing() =>
        Assert.That(AuditSharingClassifier.Hash(null), Is.Null);

    static HashedStorageIdentity Identity(string engine = "SQLServer", string server = "server", string database = "database", string schema = "schema") =>
        new(engine, IdentityHash.Compute(server), IdentityHash.Compute(database), schema is null ? null : IdentityHash.Compute(schema));
}
