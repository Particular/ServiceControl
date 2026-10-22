namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;

    [TestFixture]
    class SchemaConfigurationTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Absent_or_blank_schema_falls_back_to_psw_audit(string configured)
        {
            var settings = CreateSettings();
            if (configured != null)
            {
                settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = configured;
            }

            Assert.That(EFPersistenceConfigurationBase.CreateSettings(settings).Schema, Is.EqualTo("psw_audit"));
        }

        [Test]
        public void Configured_schema_is_used()
        {
            var settings = CreateSettings();
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = "legacy";

            Assert.That(EFPersistenceConfigurationBase.CreateSettings(settings).Schema, Is.EqualTo("legacy"));
        }

        static PersistenceSettings CreateSettings()
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = "Server=nowhere";
            return settings;
        }
    }
}
