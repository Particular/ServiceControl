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

    [TearDown]
    public void ClearEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_THROTTLEPAUSEMILLISECONDS", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDPERCENT", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDMINIMUM", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", null);
    }

    [Test]
    public void Defaults_match_the_contract_when_nothing_is_configured()
    {
        var options = MigrationEngineOptions.FromSettings(Namespace);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.ThrottlePause, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(options.HaltThresholdPercent, Is.EqualTo(5));
            Assert.That(options.HaltThresholdMinimum, Is.EqualTo(100));
            Assert.That(options.SelectedOptionalCategoryIds, Is.Empty);
            Assert.That(options.BodyRetryBackoff, Is.EqualTo(TimeSpan.FromMilliseconds(200)));
        }
    }

    [Test]
    public void Reads_configured_values_from_environment_variables()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_THROTTLEPAUSEMILLISECONDS", "2500");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDPERCENT", "10");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_HALTTHRESHOLDMINIMUM", "50");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "EventLog, CustomChecks");

        var options = MigrationEngineOptions.FromSettings(Namespace);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.ThrottlePause, Is.EqualTo(TimeSpan.FromMilliseconds(2500)));
            Assert.That(options.HaltThresholdPercent, Is.EqualTo(10));
            Assert.That(options.HaltThresholdMinimum, Is.EqualTo(50));
            Assert.That(options.SelectedOptionalCategoryIds, Is.EquivalentTo(new[] { "EventLog", "CustomChecks" }));
        }
    }

    [Test]
    public void Refuses_an_unknown_optional_category_id()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "NoSuchCategory");

        var ex = Assert.Throws<InvalidOperationException>(() => MigrationEngineOptions.FromSettings(Namespace));

        Assert.That(ex.Message, Does.Contain("NoSuchCategory"));
    }

    [Test]
    public void Refuses_a_required_category_id_named_as_optional()
    {
        // EndpointSettings is required, not optional: naming it here is a customer mistake, not
        // a valid way to force it. Required categories are never a matter of configuration.
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "EndpointSettings");

        var ex = Assert.Throws<InvalidOperationException>(() => MigrationEngineOptions.FromSettings(Namespace));

        Assert.That(ex.Message, Does.Contain("EndpointSettings"));
    }
}
