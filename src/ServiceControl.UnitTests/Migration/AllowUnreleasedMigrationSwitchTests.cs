#if DEBUG
namespace ServiceControl.UnitTests.Migration;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Migration.Checks;

[TestFixture]
class AllowUnreleasedMigrationSwitchTests
{
    [TestCase("true")]
    [TestCase("TRUE")]
    public void The_switch_registers_the_marker_when_the_variable_is_true(string value)
    {
        var services = new ServiceCollection();

        RunCommand.AllowUnreleasedMigrationWhenAsked(services, value);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetService<AllowUnreleasedMigration>(), Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("false")]
    public void The_switch_registers_nothing_unless_the_variable_is_true(string value)
    {
        var services = new ServiceCollection();

        RunCommand.AllowUnreleasedMigrationWhenAsked(services, value);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetService<AllowUnreleasedMigration>(), Is.Null);
    }
}
#endif
