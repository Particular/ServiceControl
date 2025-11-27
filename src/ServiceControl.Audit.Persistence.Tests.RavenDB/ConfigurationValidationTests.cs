namespace ServiceControl.UnitTests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.RavenDB;

    class ConfigurationValidationTests
    {
        [Test]
        public void Should_apply_persistence_settings()
        {
            var settings = BuildSettings();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["RavenPersistenceConfiguration.ConnectionStringKey"] = "connection string",
                })
                .Build();

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.AuditRetentionPeriod, Is.EqualTo(settings.AuditRetentionPeriod));
                Assert.That(configuration.MaxBodySizeToStore, Is.EqualTo(settings.MaxBodySizeToStore));
                Assert.That(configuration.EnableFullTextSearch, Is.EqualTo(settings.EnableFullTextSearchOnBodies));
            });
        }

        [Test]
        public void Should_support_external_server()
        {
            var settings = BuildSettings();
            var connectionString = "http://someserver:44444";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [RavenPersistenceConfiguration.ConnectionStringKey] = connectionString,
                })
                .Build();

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings,config);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.False);
                Assert.That(configuration.ServerConfiguration.ConnectionString, Is.EqualTo(connectionString));
            });
        }

        [Test]
        public void Should_support_embedded_server()
        {
            var settings = BuildSettings();
            var dpPath = "c://db-path";
            var logPath = "c://log-path";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [RavenPersistenceConfiguration.DatabasePathKey] = dpPath,
                    [RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = "11111",
                    [RavenPersistenceConfiguration.LogPathKey] = logPath,
                })
                .Build();

            var configuration = RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ServerConfiguration.UseEmbeddedServer, Is.True);
                Assert.That(configuration.ServerConfiguration.DbPath, Is.EqualTo(dpPath));
                Assert.That(configuration.ServerConfiguration.LogPath, Is.EqualTo(logPath));
                Assert.That(configuration.ServerConfiguration.ServerUrl, Is.EqualTo("http://localhost:11111"));
            });
        }

        [Test]
        public void Should_throw_if_port_is_missing()
        {
            var settings = BuildSettings();
            var dpPath = "c://some-path";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [RavenPersistenceConfiguration.DatabasePathKey] = dpPath,
                })
                .Build();

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config));
        }

        [Test]
        public void Should_throw_if_port_is_not_an_integer()
        {
            var settings = BuildSettings();
            var dpPath = "c://some-path";


            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [RavenPersistenceConfiguration.DatabasePathKey] = dpPath,
                    [RavenPersistenceConfiguration.DatabaseMaintenancePortKey] = "not an int",
                })
                .Build();

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config));
        }

        [Test]
        public void Should_throw_if_no_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();
            var config = new ConfigurationBuilder().Build();

            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config));
        }

        [Test]
        public void Should_throw_if_both_path_or_connection_string_is_configured()
        {
            var settings = BuildSettings();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    [RavenPersistenceConfiguration.DatabasePathKey] = "path",
                    [RavenPersistenceConfiguration.ConnectionStringKey] = "connection string",
                })
                .Build();


            Assert.Throws<InvalidOperationException>(() => RavenPersistenceConfiguration.GetDatabaseConfiguration(settings, config));
        }

        PersistenceSettings BuildSettings()
        {
            return new PersistenceSettings(TimeSpan.FromMinutes(2), true, 100000);
        }
    }
}