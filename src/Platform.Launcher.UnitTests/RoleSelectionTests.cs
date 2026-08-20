namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class RoleSelectionTests
{
    [Test]
    public void Missing_value_defaults_to_primary()
    {
        var selection = RoleSelection.Parse(null);

        Assert.That(selection.ProcessRoles, Is.EqualTo(new[] { ContainerRole.Primary }));
        Assert.That(selection.Capabilities, Is.Empty);
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("Primary,,Audit")]
    [TestCase(",Primary")]
    public void Empty_values_are_rejected(string value)
    {
        var exception = Assert.Throws<LauncherConfigurationException>(() => RoleSelection.Parse(value));

        Assert.That(exception!.Message, Does.Contain("Allowed values: Primary, Audit, Monitoring, ServicePulse, All"));
    }

    [Test]
    public void Values_are_trimmed_case_insensitive_deduplicated_and_canonically_ordered()
    {
        var selection = RoleSelection.Parse(" monitoring,PRIMARY, audit,primary ");

        Assert.That(selection.ProcessRoles, Is.EqualTo(new[]
        {
            ContainerRole.Primary,
            ContainerRole.Audit,
            ContainerRole.Monitoring
        }));
    }

    [Test]
    public void ServicePulse_is_a_capability_that_implies_primary()
    {
        var selection = RoleSelection.Parse("ServicePulse");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.ProcessRoles, Is.EqualTo(new[] { ContainerRole.Primary }));
            Assert.That(selection.Capabilities, Is.EqualTo(new[] { ContainerCapability.ServicePulse }));
        }
    }

    [Test]
    public void All_expands_to_every_process_role_and_service_pulse()
    {
        var selection = RoleSelection.Parse("All");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.ProcessRoles, Is.EqualTo(Enum.GetValues<ContainerRole>()));
            Assert.That(selection.Capabilities, Is.EqualTo(new[] { ContainerCapability.ServicePulse }));
        }
    }

    [TestCase("Unknown")]
    [TestCase("1")]
    public void Unknown_values_are_rejected_with_the_complete_allowed_list(string value)
    {
        var exception = Assert.Throws<LauncherConfigurationException>(() => RoleSelection.Parse(value));

        Assert.That(exception!.Message, Is.EqualTo(
            $"Invalid SERVICE_CONTROL_ROLE. Unknown role '{value}'. Allowed values: Primary, Audit, Monitoring, ServicePulse, All."));
    }
}