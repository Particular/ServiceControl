namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;
    using ServiceControl.PostgreSql.TokenAuthentication;

    [TestFixture]
    class PostgreSqlTokenAuthenticationTests
    {
        [Test]
        public void Leaves_a_connection_string_without_token_keywords_untouched()
        {
            const string configured = "host=db.example.com; Username = sc ;Password='p;w';Application Name=\"ServiceControl\"";

            var settings = new PostgreSqlPersisterSettings(new EFPersisterSettings
            {
                ConnectionString = configured,
                AuditRetentionPeriod = TimeSpan.FromDays(1),
                MaxBodySizeToStore = 100000
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.ConnectionString, Is.EqualTo(configured));
                Assert.That(settings.TokenAuthentication, Is.Null);
            }
        }

        [Test]
        public void Keeps_every_other_setting()
        {
            var configured = new EFPersisterSettings
            {
                ConnectionString = "Host=db.example.com;Username=sc;Token Authentication=AwsRdsIam",
                Schema = "sc_custom",
                CommandTimeout = 41,
                QueryTimeout = TimeSpan.FromSeconds(42),
                AuditRetentionPeriod = TimeSpan.FromDays(43),
                MaxBodySizeToStore = 44,
                MaxRetryCount = 45,
                MaxRetryDelayInSeconds = 46,
                EnableRetryOnFailure = false,
                EnableSensitiveDataLogging = true
            };

            var settings = new PostgreSqlPersisterSettings(configured);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.ConnectionString, Does.Not.Contain("Token").IgnoreCase);
                Assert.That(settings.TokenAuthentication, Is.InstanceOf<AwsRdsIamTokenAuthentication>());
                Assert.That(settings.Schema, Is.EqualTo(configured.Schema));
                Assert.That(settings.CommandTimeout, Is.EqualTo(configured.CommandTimeout));
                Assert.That(settings.QueryTimeout, Is.EqualTo(configured.QueryTimeout));
                Assert.That(settings.AuditRetentionPeriod, Is.EqualTo(configured.AuditRetentionPeriod));
                Assert.That(settings.MaxBodySizeToStore, Is.EqualTo(configured.MaxBodySizeToStore));
                Assert.That(settings.MaxRetryCount, Is.EqualTo(configured.MaxRetryCount));
                Assert.That(settings.MaxRetryDelayInSeconds, Is.EqualTo(configured.MaxRetryDelayInSeconds));
                Assert.That(settings.EnableRetryOnFailure, Is.EqualTo(configured.EnableRetryOnFailure));
                Assert.That(settings.EnableSensitiveDataLogging, Is.EqualTo(configured.EnableSensitiveDataLogging));
            }
        }

        [Test]
        public void Rejects_invalid_keywords_when_the_persistence_is_created()
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = "Host=db.example.com;Token Authentication=AwsRdsIam";

            var exception = Assert.Throws<InvalidOperationException>(() => new PostgreSqlPersistenceConfiguration().Create(settings));

            Assert.That(exception.Message, Does.StartWith("Database/ConnectionString is invalid.").And.Contain("Username"));
        }
    }
}
