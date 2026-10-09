namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.RavenDB;

[TestFixture]
[NonParallelizable]
class RavenEventsRetentionConfigurationTests
{
    const string DocumentedVariable = "SERVICECONTROL_EVENTRETENTIONPERIOD";
    const string LegacyVariable = "SERVICECONTROL_EVENTSRETENTIONPERIOD";
    const string ErrorRetentionVariable = "SERVICECONTROL_ERRORRETENTIONPERIOD";

    [SetUp]
    public void SetUp() => Environment.SetEnvironmentVariable(ErrorRetentionVariable, "10.00:00:00");

    [TearDown]
    public void TearDown()
    {
        foreach (var variable in new[] { DocumentedVariable, LegacyVariable, ErrorRetentionVariable })
        {
            Environment.SetEnvironmentVariable(variable, null);
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

    static RavenPersisterSettings CreateSettings() =>
        (RavenPersisterSettings)new RavenPersistenceConfiguration().CreateSettings(new SettingsRootNamespace("ServiceControl"));
}
