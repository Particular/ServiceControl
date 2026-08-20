namespace ServiceControl.UnitTests.Hosting
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Hosting.Commands;

    // Environment variables are process wide, so these cannot run alongside anything else that reads them.
    [TestFixture]
    [NonParallelizable]
    public class AuditIngestionOnlyCommandTests
    {
        [TestCase("RavenDB")]
        [TestCase("SQLServer")]
        [TestCase("PostgreSQL")]
        public void Should_refuse_to_start_against_storage_without_audit_support(string persistenceType)
        {
            var settings = CreateSettings(persistenceType);

            var exception = Assert.ThrowsAsync<Exception>(() =>
                new AuditIngestionOnlyCommand().Execute(new HostArguments([]), settings));

            Assert.That(exception.Message, Does.Contain("supports audit ingestion"));
        }

        [Test]
        public void Should_refuse_to_combine_the_two_ingestion_only_modes()
        {
            var exception = Assert.Throws<Exception>(() =>
                IngestionOnlyGuards.EnsureModesAreNotCombined(errorIngestionOnly: true, auditIngestionOnly: true));

            Assert.That(exception.Message, Does.Contain("cannot be combined"));
        }

        [Test]
        public void Should_refuse_file_system_body_storage_that_is_not_asserted_as_shared()
        {
            using var _ = new EnvironmentVariableScope("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");

            var exception = Assert.Throws<Exception>(() =>
                IngestionOnlyGuards.EnsureBodyStorageIsReadableByEveryHost("--audit-ingestion-only"));

            Assert.That(exception.Message, Does.Contain(IngestionOnlyGuards.SharedBodyStoragePathKey));
        }

        [Test]
        public void Should_accept_file_system_body_storage_asserted_as_shared()
        {
            using var storageType = new EnvironmentVariableScope("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
            using var pathIsShared = new EnvironmentVariableScope("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_PATHISSHARED", "true");

            Assert.DoesNotThrow(() =>
                IngestionOnlyGuards.EnsureBodyStorageIsReadableByEveryHost("--audit-ingestion-only"));
        }

        [Test]
        public void Should_ignore_body_storage_that_every_host_can_already_read()
        {
            using var _ = new EnvironmentVariableScope("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "AzureBlob");

            Assert.DoesNotThrow(() =>
                IngestionOnlyGuards.EnsureBodyStorageIsReadableByEveryHost("--audit-ingestion-only"));
        }

        static Settings CreateSettings(string persistenceType) =>
            new("LearningTransport", persistenceType, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

        sealed class EnvironmentVariableScope : IDisposable
        {
            readonly string name;

            public EnvironmentVariableScope(string name, string value)
            {
                this.name = name;
                Environment.SetEnvironmentVariable(name, value);
            }

            public void Dispose() => Environment.SetEnvironmentVariable(name, null);
        }
    }
}
