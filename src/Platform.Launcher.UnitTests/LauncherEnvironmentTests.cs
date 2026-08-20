namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class LauncherEnvironmentTests
{
    [Test]
    public void Run_in_place_uses_each_role_project_output_directory()
    {
        var launcherBaseDirectory = Path.Combine(Path.GetTempPath(), "repo", "src", "Platform.Launcher", "bin", "Debug", "net10.0");

        var descriptors = RoleDescriptor.CreateDevelopment(launcherBaseDirectory, "Debug");

        var sourceRoot = Path.Combine(Path.GetTempPath(), "repo", "src");
        var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(descriptors[0].WorkingDirectory,
                Is.EqualTo(Path.Combine(sourceRoot, "ServiceControl", "bin", "Debug", "net10.0")));
            Assert.That(descriptors[0].ExecutablePath,
                Is.EqualTo(Path.Combine(descriptors[0].WorkingDirectory, $"ServiceControl{executableSuffix}")));
            Assert.That(descriptors[1].WorkingDirectory,
                Is.EqualTo(Path.Combine(sourceRoot, "ServiceControl.Audit", "bin", "Debug", "net10.0")));
            Assert.That(descriptors[2].WorkingDirectory,
                Is.EqualTo(Path.Combine(sourceRoot, "ServiceControl.Monitoring", "bin", "Debug", "net10.0")));
        }
    }

    [Test]
    public void Run_in_place_preserves_the_output_shape_used_by_development_plugin_discovery()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "repo", "src");
        var launcherBaseDirectory = Path.Combine(sourceRoot, "Platform.Launcher", "bin", "Debug", "net10.0");

        var primary = RoleDescriptor.CreateDevelopment(launcherBaseDirectory, "Debug")[0];
        var sourceRootDiscoveredByPluginLoader = Path.GetFullPath(Path.Combine(primary.WorkingDirectory, "..", "..", "..", ".."));

        Assert.That(sourceRootDiscoveredByPluginLoader, Is.EqualTo(sourceRoot));
    }

    [Test]
    public void Disabled_run_in_place_uses_the_configured_application_root()
    {
        var descriptors = LauncherEnvironment.CreateRoleDescriptors("false", "/custom-app", "/ignored");

        Assert.That(descriptors[0].WorkingDirectory, Is.EqualTo(Path.Combine("/custom-app", "primary")));
    }

    [Test]
    public void Invalid_run_in_place_value_is_rejected()
    {
        var exception = Assert.Throws<LauncherConfigurationException>(() =>
            LauncherEnvironment.CreateRoleDescriptors("sometimes", null, "/ignored"));

        Assert.That(exception!.Message, Is.EqualTo("SERVICECONTROL_LAUNCHER_RUN_IN_PLACE must be 'true' or 'false'."));
    }
}