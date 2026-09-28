namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

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
            return settings;
        }
    }
}
