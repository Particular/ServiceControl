namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.IO;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControl.Persistence;

    [TestFixture]
    public class PersistenceManifestAuditCapabilityTests
    {
        [Test]
        public void Absent_property_means_no_audit_support()
        {
            var manifest = Deserialize("""
                {
                  "Name": "Whatever",
                  "DisplayName": "Whatever",
                  "Description": "Whatever",
                  "AssemblyName": "Whatever",
                  "TypeName": "Whatever, Whatever"
                }
                """);

            Assert.That(manifest.SupportsAuditIngestion, Is.False);
        }

        [TestCase("ServiceControl.Persistence.RavenDB")]
        [TestCase("ServiceControl.Persistence.EFCore.SqlServer")]
        [TestCase("ServiceControl.Persistence.EFCore.PostgreSql")]
        public void Shipped_primary_persisters_do_not_advertise_audit_support(string projectName)
        {
            var manifest = ReadManifest(projectName);

            Assert.That(manifest.SupportsAuditIngestion, Is.False,
                $"{projectName} advertises audit ingestion, which makes the primary host ingest the audit queue. "
                + "Only flip this once that persister can store and query audit data.");
        }

        [Test]
        public void The_test_persister_advertises_audit_support()
        {
            var manifest = ReadManifest("ServiceControl.Persistence.Tests.AuditCapable");

            Assert.That(manifest.SupportsAuditIngestion, Is.True);
        }

        static PersistenceManifest ReadManifest(string projectName) =>
            Deserialize(File.ReadAllText(Path.Combine(SourceDirectory, projectName, "persistence.manifest")));

        static PersistenceManifest Deserialize(string json) =>
            JsonSerializer.Deserialize<PersistenceManifest>(json) ?? throw new InvalidOperationException("The manifest is empty or invalid.");

        static string SourceDirectory =>
            Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
    }
}
