namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class LaunchPlanTests
{
    [Test]
    public void Child_launches_follow_canonical_order_and_receive_unchanged_arguments()
    {
        var selection = RoleSelection.Parse("Monitoring,Primary,Audit");
        var command = ContainerCommand.Parse(["run", "--setup-and-run"], selection.ProcessRoles.Count);

        var plan = LaunchPlan.Create(selection, command, RoleDescriptor.Create("/test-app"), EmptyEnvironment());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.Children.Select(child => child.Descriptor.Role), Is.EqualTo(Enum.GetValues<ContainerRole>()));
            Assert.That(plan.Children, Has.All.Matches<ChildLaunch>(child => child.Arguments.SequenceEqual(["--setup-and-run"])));
        }
    }

    [Test]
    public void ServicePulse_sets_the_primary_child_environment_when_unset()
    {
        var selection = RoleSelection.Parse("All");
        var command = ContainerCommand.Parse([], selection.ProcessRoles.Count);

        var plan = LaunchPlan.Create(selection, command, RoleDescriptor.Create("/test-app"), EmptyEnvironment());

        var primary = plan.Children.Single(child => child.Descriptor.Role == ContainerRole.Primary);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(primary.EnvironmentOverrides, Contains.Key("SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE"));
            Assert.That(primary.EnvironmentOverrides["SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE"], Is.EqualTo("true"));
            Assert.That(plan.Children.Where(child => child.Descriptor.Role != ContainerRole.Primary),
                Has.All.Matches<ChildLaunch>(child => child.EnvironmentOverrides.Count == 0));
        }
    }

    [Test]
    public void Explicitly_enabled_integrated_service_pulse_is_preserved()
    {
        var environment = new Dictionary<string, string?>
        {
            ["servicecontrol_enableintegratedservicepulse"] = "TRUE",
            ["MONITORING_URL"] = "https://example.test/monitoring"
        };

        var plan = CreateServicePulsePlan(environment);

        Assert.That(plan.Children.Single().EnvironmentOverrides, Is.Empty);
    }

    [TestCase("false")]
    [TestCase("FALSE")]
    public void Explicitly_disabled_integrated_service_pulse_is_rejected(string value)
    {
        var environment = new Dictionary<string, string?>
        {
            ["SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE"] = value
        };

        var exception = Assert.Throws<LauncherConfigurationException>(() => CreateServicePulsePlan(environment));

        Assert.That(exception!.Message, Does.Contain("requires SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE=true"));
    }

    [Test]
    public void Role_descriptors_use_the_injected_application_root()
    {
        var descriptors = RoleDescriptor.Create(Path.Combine("root", "apps"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(descriptors[0], Is.EqualTo(new RoleDescriptor(
                ContainerRole.Primary,
                Path.Combine("root", "apps", "primary", "ServiceControl"),
                Path.Combine("root", "apps", "primary"),
                new Uri("http://localhost:33333/api/configuration"),
                33333)));
            Assert.That(descriptors[1].ExecutablePath, Is.EqualTo(Path.Combine("root", "apps", "audit", "ServiceControl.Audit")));
            Assert.That(descriptors[2].HealthEndpoint, Is.EqualTo(new Uri("http://localhost:33633/connection")));
        }
    }

    static LaunchPlan CreateServicePulsePlan(IReadOnlyDictionary<string, string?> environment)
    {
        var selection = RoleSelection.Parse("ServicePulse");
        var command = ContainerCommand.Parse([], selection.ProcessRoles.Count);
        return LaunchPlan.Create(selection, command, RoleDescriptor.Create("/test-app"), environment);
    }

    static IReadOnlyDictionary<string, string?> EmptyEnvironment() => new Dictionary<string, string?>();
}