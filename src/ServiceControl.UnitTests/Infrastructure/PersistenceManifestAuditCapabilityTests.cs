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

        [Test]
        public void RavenDB_does_not_advertise_audit_support()
        {
            var manifest = ReadManifest("ServiceControl.Persistence.RavenDB");

            Assert.That(manifest.SupportsAuditIngestion, Is.False,
                "RavenDB audit stays in the standalone audit instance; advertising support here would make the primary host ingest the audit queue.");
        }

        [TestCase("ServiceControl.Persistence.EFCore.SqlServer")]
        [TestCase("ServiceControl.Persistence.EFCore.PostgreSql")]
        public void The_relational_persisters_advertise_audit_support(string projectName)
        {
            var manifest = ReadManifest(projectName);

            Assert.That(manifest.SupportsAuditIngestion, Is.True,
                $"{projectName} stores and queries audit data, so the primary host ingests the audit queue on it.");
        }

        static PersistenceManifest ReadManifest(string projectName) =>
            Deserialize(File.ReadAllText(Path.Combine(SourceDirectory, projectName, "persistence.manifest")));

        static PersistenceManifest Deserialize(string json) =>
            JsonSerializer.Deserialize<PersistenceManifest>(json) ?? throw new InvalidOperationException("The manifest is empty or invalid.");

        static string SourceDirectory =>
            Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
    }
}
