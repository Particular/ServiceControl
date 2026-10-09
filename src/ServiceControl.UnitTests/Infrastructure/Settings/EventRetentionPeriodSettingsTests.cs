namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;

[TestFixture]
[NonParallelizable]
class EventRetentionPeriodSettingsTests
{
    const string DocumentedVariable = "SERVICECONTROL_EVENTRETENTIONPERIOD";
    const string LegacyVariable = "SERVICECONTROL_EVENTSRETENTIONPERIOD";

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(DocumentedVariable, null);
        Environment.SetEnvironmentVariable(LegacyVariable, null);
    }

    [Test]
    public void Reports_the_spelling_existing_installs_use()
    {
        Environment.SetEnvironmentVariable(LegacyVariable, "5.00:00:00");

        Assert.That(new Settings().EventsRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(5)));
    }

    [Test]
    public void Prefers_the_documented_setting_over_the_existing_spelling()
    {
        Environment.SetEnvironmentVariable(DocumentedVariable, "3.00:00:00");
        Environment.SetEnvironmentVariable(LegacyVariable, "5.00:00:00");

        Assert.That(new Settings().EventsRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(3)));
    }
}
