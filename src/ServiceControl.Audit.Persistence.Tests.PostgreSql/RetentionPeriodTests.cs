namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;
    using ServiceControl.Persistence.BodyStorage;

    [TestFixture]
    class RetentionPeriodTests
    {
        [TestCase("23:59:00")]
        [TestCase("90.00:01:00")]
        public void Refuses_a_retention_period_outside_one_to_ninety_days(string retentionPeriod)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new PostgreSqlPersistenceConfiguration().Create(SettingsWith(TimeSpan.Parse(retentionPeriod))));

            Assert.That(exception.Message, Does.Contain("1 to 90 days"));
        }

        [TestCase("1.00:00:00")]
        [TestCase("90.00:00:00")]
        public void Accepts_a_retention_period_of_one_to_ninety_days(string retentionPeriod) =>
            Assert.That(new PostgreSqlPersistenceConfiguration().Create(SettingsWith(TimeSpan.Parse(retentionPeriod))), Is.Not.Null);

        static PersistenceSettings SettingsWith(TimeSpan retentionPeriod)
        {
            var settings = new PersistenceSettings(retentionPeriod, true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = "Host=localhost";
            settings.PersisterSpecificSettings[BodyStorageSettingsParser.StorageTypeKey] = nameof(BodyStorageType.FileSystem);
            settings.PersisterSpecificSettings[BodyStorageSettingsParser.FileSystemStoragePathKey] = Path.GetTempPath();
            return settings;
        }
    }
}
