namespace ServiceControl.Audit.Persistence.Tests.RavenDB;

using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Persistence.Tests;

[TestFixture]
class RavenFlavourTests
{
    [Test]
    public void The_selected_persister_matches_the_RavenDB_client_under_test()
    {
        var manifest = PersistenceManifestLibrary.Find(RavenFlavour.PersistenceType);

        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest.DisplayName, Is.EqualTo($"RavenDB {typeof(DocumentStore).Assembly.GetName().Version.Major}"));
    }
}
