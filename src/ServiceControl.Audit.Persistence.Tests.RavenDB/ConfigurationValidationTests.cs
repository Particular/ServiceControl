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

            Assert.AreEqual(settings.AuditRetentionPeriod, configuration.AuditRetentionPeriod);
            Assert.AreEqual(settings.MaxBodySizeToStore, configuration.MaxBodySizeToStore);
            Assert.AreEqual(settings.EnableFullTextSearchOnBodies, configuration.EnableFullTextSearch);
        }

        [Test]
        public void Should_support_external_server()
        {
            var settings = BuildSettings();
            var connectionString = "http://someserver:44444";

            settings.PersisterSpecificSettings[RavenPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.False);
            Assert.AreEqual(connectionString, configuration.ServerConfiguration.ConnectionString);
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

            Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.True);
            Assert.AreEqual(dpPath, configuration.ServerConfiguration.DbPath);
            Assert.AreEqual(logPath, configuration.ServerConfiguration.LogPath);
            Assert.AreEqual("http://localhost:11111", configuration.ServerConfiguration.ServerUrl);
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

        PersistenceSettings BuildSettings()
        {
            return new PersistenceSettings(TimeSpan.FromMinutes(2), true, 100000);
        }
    }
}