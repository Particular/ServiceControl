namespace ServiceControl.Audit.Persistence.Tests.MongoDB
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.MongoDB;

    [TestFixture]
    class ConfigurationValidationTests
    {
        [Test]
        public void Should_apply_persistence_settings()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb://localhost:27017/testdb";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.Multiple(() =>
            {
                Assert.That(mongoSettings.AuditRetentionPeriod, Is.EqualTo(settings.AuditRetentionPeriod));
                Assert.That(mongoSettings.MaxBodySizeToStore, Is.EqualTo(settings.MaxBodySizeToStore));
                Assert.That(mongoSettings.EnableFullTextSearchOnBodies, Is.EqualTo(settings.EnableFullTextSearchOnBodies));
            });
        }

        [Test]
        public void Should_extract_database_name_from_connection_string()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb://localhost:27017/myauditdb";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.DatabaseName, Is.EqualTo("myauditdb"));
        }

        [Test]
        public void Should_default_database_name_to_audit_when_not_in_connection_string()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb://localhost:27017";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.DatabaseName, Is.EqualTo("audit"));
        }

        [Test]
        public void Should_extract_database_name_from_connection_string_with_options()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb://localhost:27017/customdb?retryWrites=true&w=majority";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.DatabaseName, Is.EqualTo("customdb"));
        }

        [Test]
        public void Should_handle_replica_set_connection_string()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb://host1:27017,host2:27017,host3:27017/auditdb?replicaSet=rs0";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.DatabaseName, Is.EqualTo("auditdb"));
        }

        [Test]
        public void Should_handle_mongodb_srv_connection_string()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                "mongodb+srv://user:pass@cluster.mongodb.net/production";

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.DatabaseName, Is.EqualTo("production"));
        }

        [Test]
        public void Should_throw_if_connection_string_is_missing()
        {
            var settings = BuildSettings();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                MongoPersistenceConfiguration.GetMongoSettings(settings));

            Assert.That(ex.Message, Does.Contain(MongoPersistenceConfiguration.ConnectionStringKey));
        }

        [Test]
        public void Should_throw_if_connection_string_is_empty()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = "";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                MongoPersistenceConfiguration.GetMongoSettings(settings));

            Assert.That(ex.Message, Does.Contain("cannot be empty"));
        }

        [Test]
        public void Should_throw_if_connection_string_is_whitespace()
        {
            var settings = BuildSettings();
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = "   ";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                MongoPersistenceConfiguration.GetMongoSettings(settings));

            Assert.That(ex.Message, Does.Contain("cannot be empty"));
        }

        [Test]
        public void Should_preserve_connection_string()
        {
            var settings = BuildSettings();
            var connectionString = "mongodb://localhost:27017/testdb";
            settings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var mongoSettings = MongoPersistenceConfiguration.GetMongoSettings(settings);

            Assert.That(mongoSettings.ConnectionString, Is.EqualTo(connectionString));
        }

        static PersistenceSettings BuildSettings()
        {
            return new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
        }
    }
}
