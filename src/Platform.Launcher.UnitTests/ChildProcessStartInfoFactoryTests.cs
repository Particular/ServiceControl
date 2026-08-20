namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class ChildProcessStartInfoFactoryTests
{
    [Test]
    public void Child_arguments_and_environment_overrides_are_forwarded_without_a_shell()
    {
        var descriptor = new RoleDescriptor(
            ContainerRole.Primary,
            Path.Combine("test-app", "primary", "ServiceControl"),
            Path.Combine("test-app", "primary"),
            new Uri("http://localhost:33333/api/configuration"),
            33333);
        var child = new ChildLaunch(
            descriptor,
            ["--import-failed-errors", "file name.zip", "--value=quoted \"text\""],
            new Dictionary<string, string>
            {
                ["SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE"] = "true"
            });

        var startInfo = ChildProcessStartInfoFactory.Create(child);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(startInfo.FileName, Is.EqualTo(descriptor.ExecutablePath));
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo(descriptor.WorkingDirectory));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(child.Arguments));
            Assert.That(startInfo.Environment["SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE"], Is.EqualTo("true"));
        }
    }
}