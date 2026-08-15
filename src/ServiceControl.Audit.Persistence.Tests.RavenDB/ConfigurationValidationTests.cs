namespace ServiceControl.UnitTests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.RavenDB;

    class ConfigurationValidationTests
    {
        [Test]
        public void Should_apply_persistence_settings()
        {
            var settings = BuildSettings();

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = "connection string";

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(configuration.AuditRetentionPeriod, Is.EqualTo(settings.AuditRetentionPeriod));
                Assert.That(configuration.MaxBodySizeToStore, Is.EqualTo(settings.MaxBodySizeToStore));
                Assert.That(configuration.EnableFullTextSearch, Is.EqualTo(settings.EnableFullTextSearchOnBodies));
            }
        }

        [Test]
        public void Should_support_external_server()
        {
            var settings = BuildSettings();
            var connectionString = "http://someserver:44444";

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.False);
                Assert.That(configuration.ServerConfiguration.ConnectionString, Is.EqualTo(connectionString));
            }
        }

        [Test]
        public void Should_support_embedded_server()
        {
            var settings = BuildSettings();
            var dpPath = "c://db-path";
            var logPath = "c://log-path";

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = dpPath;
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = "11111";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.LogPathKey] = logPath;
            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.True);
                Assert.That(configuration.ServerConfiguration.DbPath, Is.EqualTo(dpPath));
                Assert.That(configuration.ServerConfiguration.LogPath, Is.EqualTo(logPath));
                Assert.That(configuration.ServerConfiguration.ServerUrl, Is.EqualTo("http://localhost:11111"));
            }
        }

        [Test]
        public void Should_throw_if_port_is_missing()
        {
            var settings = BuildSettings();
            var dpPath = "c://some-path";

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = dpPath;

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        [Test]
        public void Should_throw_if_port_is_not_an_integer()
        {
            var settings = BuildSettings();
            var dpPath = "c://some-path";

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = dpPath;
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = "not an int";

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        [Test]
        public void Should_throw_if_no_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        [Test]
        public void Should_throw_if_both_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabasePathKey] = "path";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = "connection string";

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        [Test]
        public void Should_throw_when_bucket_mode_is_enabled_and_expiration_process_timer_is_zero()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = "connection string";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.EnableAuditRetentionBucketsKey] = "true";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ExpirationProcessTimerInSecondsKey] = "0";

            var exception = Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.ExpirationProcessTimerInSecondsKey));
                Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.EnableAuditRetentionBucketsKey));
            });
        }

        [Test]
        public void Should_accept_zero_expiration_process_timer_when_bucket_mode_is_disabled()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = "connection string";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ExpirationProcessTimerInSecondsKey] = "0";

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.That(configuration.ExpirationProcessTimerInSeconds, Is.EqualTo(0));
        }

        [Test]
        public void Should_default_bucket_duration_to_one_hour()
        {
            var settings = BuildBucketModeSettings();

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.That(configuration.AuditRetentionBucketDuration, Is.EqualTo(TimeSpan.FromHours(1)));
        }

        [Test]
        public void Should_apply_configured_bucket_duration()
        {
            var settings = BuildBucketModeSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.AuditRetentionBucketDurationKey] = "1.00:00:00";

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.That(configuration.AuditRetentionBucketDuration, Is.EqualTo(TimeSpan.FromDays(1)));
        }

        [Test]
        public void Should_throw_if_bucket_duration_is_not_a_time_span()
        {
            var settings = BuildBucketModeSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.AuditRetentionBucketDurationKey] = "not a time span";

            var exception = Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));

            Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.AuditRetentionBucketDurationKey));
        }

        [Test]
        public void Should_throw_if_bucket_duration_is_below_one_hour()
        {
            var settings = BuildBucketModeSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.AuditRetentionBucketDurationKey] = "00:30:00";

            var exception = Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));

            Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.AuditRetentionBucketDurationKey));
        }

        [Test]
        public void Should_throw_if_bucket_duration_is_not_a_whole_number_of_hours()
        {
            var settings = BuildBucketModeSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.AuditRetentionBucketDurationKey] = "01:30:00";

            var exception = Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));

            Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.AuditRetentionBucketDurationKey));
        }

        [Test]
        public void Should_throw_if_bucket_duration_exceeds_thirty_one_days()
        {
            var settings = BuildBucketModeSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.AuditRetentionBucketDurationKey] = "32.00:00:00";

            var exception = Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings));

            Assert.That(exception.Message, Does.Contain(RavenPersistenceConfiguration.AuditRetentionBucketDurationKey));
        }

        PersistenceSettings BuildSettings()
        {
            return new PersistenceSettings(TimeSpan.FromMinutes(2), true, 100000);
        }

        PersistenceSettings BuildBucketModeSettings()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = "connection string";
            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.EnableAuditRetentionBucketsKey] = "true";
            return settings;
        }
    }
}