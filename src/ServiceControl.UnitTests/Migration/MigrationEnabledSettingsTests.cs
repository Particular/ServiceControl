namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class MigrationEnabledSettingsTests
{
    static Settings NewSettings() =>
        new(transportType: "LearningTransport", persisterType: "RavenDB", errorRetentionPeriod: TimeSpan.FromDays(10));

    [TearDown]
    public void ClearEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ENABLED", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ALLOWINCOMPLETEEXIT", null);
    }

    [Test]
    public void Both_default_to_off()
    {
        var settings = NewSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.MigrationEnabled, Is.False);
            Assert.That(settings.MigrationAllowIncompleteExit, Is.False);
        });
    }

    [Test]
    public void MigrationEnabled_is_read_from_the_environment()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ENABLED", "true");

        Assert.That(NewSettings().MigrationEnabled, Is.True);
    }

    [Test]
    public void AllowIncompleteExit_is_read_from_the_environment()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ALLOWINCOMPLETEEXIT", "true");

        Assert.That(NewSettings().MigrationAllowIncompleteExit, Is.True);
    }

    [Test]
    public void Only_MigrationEngineOptions_parses_the_engine_settings_keys()
    {
        var parsers = typeof(Settings).Assembly.GetTypes()
            .Concat(typeof(MigrationEngineOptions).Assembly.GetTypes())
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Any(method => method.Name is "FromSettings" or "Read" or "Resolve"
                    && method.GetParameters() is [{ ParameterType.Name: nameof(SettingsRootNamespace) }]))
            .Select(type => type.Name)
            .ToArray();

        Assert.That(parsers, Is.EquivalentTo(new[] { nameof(MigrationEngineOptions) }),
            "Migration/ThrottlePauseMilliseconds and its three neighbours have exactly one parser. A second one drifts its defaults and its refusal message away from this one, and nothing fails until a customer types a category name wrong.");
    }
}
