namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
[NonParallelizable]
class EventsRetentionConfigurationTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    const string DocumentedVariable = "SERVICECONTROL_EVENTRETENTIONPERIOD";
    const string LegacyVariable = "SERVICECONTROL_EVENTSRETENTIONPERIOD";

    static readonly string[] Keys =
    [
        DocumentedVariable,
        LegacyVariable,
        "SERVICECONTROL_DATABASE_CONNECTIONSTRING",
        "SERVICECONTROL_ERRORRETENTIONPERIOD",
        "SERVICECONTROL_MESSAGEBODY_STORAGETYPE",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH"
    ];

    [SetUp]
    public void SetUp()
    {
        ClearKeys();
        Environment.SetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", "Server=nowhere");
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");
    }

    [TearDown]
    public void TearDown() => ClearKeys();

    static void ClearKeys()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Test]
    public void Applies_the_documented_setting()
    {
        Environment.SetEnvironmentVariable(DocumentedVariable, "3.00:00:00");

        Assert.That(CreateSettings().EventsRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(3)));
    }

    [Test]
    public void Applies_the_spelling_existing_installs_use()
    {
        Environment.SetEnvironmentVariable(LegacyVariable, "5.00:00:00");

        Assert.That(CreateSettings().EventsRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(5)));
    }

    [Test]
    public void Prefers_the_documented_setting_over_the_existing_spelling()
    {
        Environment.SetEnvironmentVariable(DocumentedVariable, "3.00:00:00");
        Environment.SetEnvironmentVariable(LegacyVariable, "5.00:00:00");

        Assert.That(CreateSettings().EventsRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(3)));
    }

    static EFPersisterSettings CreateSettings() =>
        (EFPersisterSettings)new TestPersistenceConfiguration().CreateSettings(TestNamespace);

    sealed class TestPersistenceConfiguration : EFPersistenceConfigurationBase
    {
        public override IPersistence Create(PersistenceSettings settings) => throw new NotSupportedException();

        protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
            new TestPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
    }

    sealed class TestPersisterSettings : EFPersisterSettings;
}
