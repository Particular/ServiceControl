namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;

[TestFixture]
[NonParallelizable]
class ExternalIntegrationsPublishingSettingsTests
{
    const string Variable = "SERVICECONTROL_ENABLEEXTERNALINTEGRATIONSPUBLISHING";
    const string LegacyVariable = "SERVICECONTROL_DISABLEEXTERNALINTEGRATIONSPUBLISHING";

    string originalValue;
    string originalLegacyValue;

    [SetUp]
    public void SetUp()
    {
        originalValue = Environment.GetEnvironmentVariable(Variable);
        originalLegacyValue = Environment.GetEnvironmentVariable(LegacyVariable);
        Environment.SetEnvironmentVariable(Variable, null);
        Environment.SetEnvironmentVariable(LegacyVariable, null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(Variable, originalValue);
        Environment.SetEnvironmentVariable(LegacyVariable, originalLegacyValue);
    }

    [Test]
    public void Is_disabled_when_neither_setting_is_configured() =>
        Assert.That(new Settings().EnableExternalIntegrationsPublishing, Is.False);

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void Reads_the_enable_setting(string value, bool expected)
    {
        Environment.SetEnvironmentVariable(Variable, value);

        Assert.That(new Settings().EnableExternalIntegrationsPublishing, Is.EqualTo(expected));
    }

    [TestCase("true", false)]
    [TestCase("false", true)]
    public void Treats_the_legacy_setting_as_the_inverse(string value, bool expected)
    {
        Environment.SetEnvironmentVariable(LegacyVariable, value);

        Assert.That(new Settings().EnableExternalIntegrationsPublishing, Is.EqualTo(expected));
    }

    [TestCase("true", "true")]
    [TestCase("true", "false")]
    [TestCase("false", "true")]
    [TestCase("false", "false")]
    public void Rejects_both_settings_being_configured(string enable, string disable)
    {
        Environment.SetEnvironmentVariable(Variable, enable);
        Environment.SetEnvironmentVariable(LegacyVariable, disable);

        var ex = Assert.Throws<Exception>(() => _ = new Settings());

        Assert.That(ex.Message, Does.Contain("cannot both be configured"));
    }
}
