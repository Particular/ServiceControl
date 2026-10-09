namespace ServiceControl.UnitTests.Migration;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class MigrationEngineOptionsTests
{
    static readonly SettingsRootNamespace Namespace = new("ServiceControl");
    static readonly TimeSpan EventRetention = TimeSpan.FromDays(3);
    static readonly TimeSpan ErrorRetention = TimeSpan.FromDays(11);

    [TearDown]
    public void ClearEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_THROTTLEPAUSEMILLISECONDS", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDPERCENT", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDMINIMUM", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ARCHIVEDANDRESOLVEDFAILEDMESSAGESWINDOW", null);
    }

    static MigrationEngineOptions Read() => MigrationEngineOptions.FromSettings(Namespace, EventRetention, ErrorRetention);

    [Test]
    public void Defaults_match_the_contract_when_nothing_is_configured()
    {
        var options = Read();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.ThrottlePause, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(options.HaltThresholdPercent, Is.EqualTo(5));
            Assert.That(options.HaltThresholdMinimum, Is.EqualTo(100));
            Assert.That(options.SelectedOptionalCategoryIds, Is.EquivalentTo(new[] { MigrationCategoryIds.EventLog, MigrationCategoryIds.ArchivedAndResolvedFailedMessages }), "both optional categories are copied unless turned off");
            Assert.That(options.EventLogWindow, Is.EqualTo(EventRetention), "the event log window defaults to the event retention period");
            Assert.That(options.ArchivedAndResolvedFailedMessagesWindow, Is.EqualTo(ErrorRetention), "the archived and resolved window defaults to the error retention period");
            Assert.That(options.BodyRetryBackoff, Is.EqualTo(TimeSpan.FromMilliseconds(200)));
        }
    }

    [Test]
    public void Reads_configured_values_from_environment_variables()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_THROTTLEPAUSEMILLISECONDS", "2500");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDPERCENT", "10");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDMINIMUM", "50");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "2.00:00:00");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ARCHIVEDANDRESOLVEDFAILEDMESSAGESWINDOW", "5.12:00:00");

        var options = Read();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.ThrottlePause, Is.EqualTo(TimeSpan.FromMilliseconds(2500)));
            Assert.That(options.HaltThresholdPercent, Is.EqualTo(10));
            Assert.That(options.HaltThresholdMinimum, Is.EqualTo(50));
            Assert.That(options.EventLogWindow, Is.EqualTo(TimeSpan.FromDays(2)));
            Assert.That(options.ArchivedAndResolvedFailedMessagesWindow, Is.EqualTo(TimeSpan.FromDays(5.5)));
            Assert.That(options.SelectedOptionalCategoryIds, Is.EquivalentTo(new[] { MigrationCategoryIds.EventLog, MigrationCategoryIds.ArchivedAndResolvedFailedMessages }));
        }
    }

    [TestCase("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", MigrationCategoryIds.ArchivedAndResolvedFailedMessages)]
    [TestCase("SERVICECONTROL_MIGRATION_ARCHIVEDANDRESOLVEDFAILEDMESSAGESWINDOW", MigrationCategoryIds.EventLog)]
    public void A_zero_window_turns_its_category_off(string variable, string stillSelected)
    {
        Environment.SetEnvironmentVariable(variable, "0");

        var options = Read();

        Assert.That(options.SelectedOptionalCategoryIds, Is.EquivalentTo(new[] { stillSelected }));
    }

    [TestCase("a week")]
    [TestCase("-1.00:00:00")]
    public void Refuses_a_window_that_is_not_a_time_span_of_zero_or_more(string value)
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", value);

        var ex = Assert.Throws<InvalidOperationException>(() => Read());

        Assert.That(ex.Message, Does.Contain(value).And.Contain(MigrationSettings.EventLogWindowKey), "the refusal has to name both the value and the setting holding it for the customer to fix it");
    }
}
