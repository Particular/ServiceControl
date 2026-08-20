namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class ContainerCommandTests
{
    [Test]
    public void Single_role_arguments_are_forwarded_unchanged()
    {
        string[] arguments = ["--import-failed-errors", "input.zip", "--some-option=value with spaces"];

        var command = ContainerCommand.Parse(arguments, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Mode, Is.EqualTo(LauncherMode.Run));
            Assert.That(command.ChildArguments, Is.EqualTo(arguments));
        }
    }

    [Test]
    public void Explicit_run_mode_is_consumed_and_remaining_arguments_are_forwarded()
    {
        var command = ContainerCommand.Parse(["run", "--setup-and-run"], 1);

        Assert.That(command.ChildArguments, Is.EqualTo(new[] { "--setup-and-run" }));
    }

    [TestCaseSource(nameof(AllowedMultipleRoleArguments))]
    public void Multiple_roles_allow_only_normal_run_and_setup_and_run(string[] arguments)
    {
        var command = ContainerCommand.Parse(arguments, 3);

        Assert.That(command.ChildArguments, Is.EqualTo(arguments.FirstOrDefault() == "run" ? arguments.Skip(1) : arguments));
    }

    [TestCase("--help")]
    [TestCase("--setup")]
    [TestCase("--setup-and-run", "extra")]
    public void Multiple_roles_reject_other_child_commands(params string[] arguments)
    {
        var exception = Assert.Throws<LauncherConfigurationException>(() => ContainerCommand.Parse(arguments, 2));

        Assert.That(exception!.Message, Does.Contain("Select one process role"));
    }

    [Test]
    public void Health_mode_is_launcher_owned()
    {
        var command = ContainerCommand.Parse(["health"], 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Mode, Is.EqualTo(LauncherMode.Health));
            Assert.That(command.ChildArguments, Is.Empty);
        }
    }

    [Test]
    public void Health_mode_rejects_application_arguments()
    {
        Assert.That(
            () => ContainerCommand.Parse(["health", "--help"], 1),
            Throws.TypeOf<LauncherConfigurationException>());
    }

    static IEnumerable<string[]> AllowedMultipleRoleArguments()
    {
        yield return [];
        yield return ["run"];
        yield return ["--setup-and-run"];
        yield return ["run", "--setup-and-run"];
    }
}