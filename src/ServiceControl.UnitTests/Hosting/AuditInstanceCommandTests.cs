namespace ServiceControl.UnitTests.Hosting
{
    using System;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Hosting.Commands;

    // Environment variables are process wide, so these cannot run alongside anything else that reads them.
    [TestFixture]
    [NonParallelizable]
    public class AuditInstanceCommandTests
    {
        [Test]
        public void Parses_the_flag_into_the_command()
        {
            var arguments = new HostArguments(["--audit-instance"]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(arguments.Command, Is.EqualTo(typeof(AuditInstanceCommand)));
                Assert.That(arguments.AuditInstance, Is.True);
            }
        }

        [Test]
        public void Setup_keeps_the_flag_so_it_provisions_the_audit_host()
        {
            var arguments = new HostArguments(["--setup", "--audit-instance"]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(arguments.Command, Is.EqualTo(typeof(SetupCommand)));
                Assert.That(arguments.AuditInstance, Is.True);
            }
        }

        [Test]
        public void Refuses_to_combine_with_an_ingestion_only_mode()
        {
            var exception = Assert.Throws<Exception>(() =>
                AuditInstanceGuards.EnsureNotCombinedWithIngestionOnly(auditInstance: true, ingestionOnly: true));

            Assert.That(exception.Message, Does.Contain("cannot be combined"));
        }

        [Test]
        public void Refuses_storage_without_audit_support()
        {
            var settings = CreateSettings("RavenDB");
            settings.ServiceControlQueueAddress = "Particular.ServiceControl";

            var exception = Assert.Throws<Exception>(() => AuditInstanceGuards.EnsureCanRun(settings));

            Assert.That(exception.Message, Does.Contain("supports audit ingestion"));
        }

        [Test]
        public void Refuses_to_run_without_the_primary_queue_to_report_to()
        {
            var settings = CreateSettings("PostgreSQL");

            var exception = Assert.Throws<Exception>(() => AuditInstanceGuards.EnsureCanRun(settings));

            Assert.That(exception.Message, Does.Contain("ServiceControlQueueAddress"));
        }

        [Test]
        public void Refuses_remote_instances_because_an_audit_host_is_a_leaf()
        {
            var settings = CreateSettings("PostgreSQL");
            settings.ServiceControlQueueAddress = "Particular.ServiceControl";
            settings.RemoteInstances = [new RemoteInstanceSetting("http://localhost:44444/api")];

            var exception = Assert.Throws<Exception>(() => AuditInstanceGuards.EnsureCanRun(settings));

            Assert.That(exception.Message, Does.Contain("remote instances"));
        }

        [Test]
        public void Reads_the_audit_data_location()
        {
            using var _ = new EnvironmentVariableScope("SERVICECONTROL_AUDITDATALOCATION", "remote");

            Assert.That(CreateSettings("PostgreSQL").AuditDataLocation, Is.EqualTo(AuditDataLocation.Remote));
        }

        [Test]
        public void Defaults_the_audit_data_location_to_local()
        {
            Assert.That(CreateSettings("PostgreSQL").AuditDataLocation, Is.EqualTo(AuditDataLocation.Local));
        }

        [Test]
        public void Rejects_an_unknown_audit_data_location()
        {
            using var _ = new EnvironmentVariableScope("SERVICECONTROL_AUDITDATALOCATION", "elsewhere");

            var exception = Assert.Throws<Exception>(() => CreateSettings("PostgreSQL"));

            Assert.That(exception.Message, Does.Contain("expected Local or Remote"));
        }

        [Test]
        public void The_host_profile_of_an_audit_instance_owns_retention_and_the_api_but_not_the_primary_endpoint()
        {
            var settings = CreateSettings("PostgreSQL");
            settings.ServiceControlQueueAddress = "Particular.ServiceControl";
            AuditInstanceCommand.ApplyMode(settings);

            var profile = settings.Host;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(profile.HostsApi, Is.True);
                Assert.That(profile.OwnsRetention, Is.True);
                Assert.That(profile.HostsPrimaryEndpoint, Is.False);
                Assert.That(profile.OwnsSingletonWork, Is.False);
                Assert.That(profile.MonitorsHeartbeats, Is.False);
                Assert.That(profile.ReportsToPrimary, Is.True);
            }
        }

        [Test]
        public void A_worker_reports_to_the_primary_only_when_given_its_queue()
        {
            var silent = CreateSettings("PostgreSQL");
            silent.AuditIngestionOnly = true;

            var reporting = CreateSettings("PostgreSQL");
            reporting.AuditIngestionOnly = true;
            reporting.ServiceControlQueueAddress = "Particular.ServiceControl";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(silent.Host.ReportsToPrimary, Is.False);
                Assert.That(reporting.Host.ReportsToPrimary, Is.True);
                Assert.That(reporting.Host.OwnsRetention, Is.False);
            }
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
