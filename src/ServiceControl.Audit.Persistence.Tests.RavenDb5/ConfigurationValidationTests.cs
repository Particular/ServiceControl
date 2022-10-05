namespace ServiceControl.UnitTests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.RavenDb;

    class ConfigurationValidationTests
    {
        [Test]
        public void Should_apply_persistence_settings()
        {
            var settings = BuildSettings();

            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.ConnectionStringKey] = "connection string";

            var configuration = RavenDbPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.AreEqual(settings.AuditRetentionPeriod, configuration.AuditRetentionPeriod);
            Assert.AreEqual(settings.MaxBodySizeToStore, configuration.MaxBodySizeToStore);
            Assert.AreEqual(settings.EnableFullTextSearchOnBodies, configuration.EnableFullTextSearch);
        }

        [Test]
        public void Should_support_external_server()
        {
            var settings = BuildSettings();
            var connectionString = "http://someserver:44444";

            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var configuration = RavenDbPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.False(configuration.ServerConfiguration.UseEmbeddedServer);
            Assert.AreEqual(connectionString, configuration.ServerConfiguration.ConnectionString);
        }

        [Test]
        public void Should_support_embedded_server()
        {
            var settings = BuildSettings();
            var dpPath = "c://some-path";

            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabasePathKey] = dpPath;
            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.HostNameKey] = "localhost";
            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabaseMaintenancePortKey] = "11111";

            var configuration = RavenDbPersistenceConfiguration.GetDatabaseConfiguration(settings);

            Assert.True(configuration.ServerConfiguration.UseEmbeddedServer);
            Assert.AreEqual(dpPath, configuration.ServerConfiguration.DbPath);
            Assert.AreEqual("http://localhost:11111", configuration.ServerConfiguration.ServerUrl);
        }

        [Test]
        public void Should_throw_if_no_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();

            Assert.Throws<InvalidOperationException>(() => RavenDbPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        [Test]
        public void Should_throw_if_both_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();

            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DatabasePathKey] = "path";
            settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.ConnectionStringKey] = "connection string";

            Assert.Throws<InvalidOperationException>(() => RavenDbPersistenceConfiguration.GetDatabaseConfiguration(settings));
        }

        PersistenceSettings BuildSettings()
        {
            return new PersistenceSettings(TimeSpan.FromMinutes(2), true, 100000);
        }
    }
}