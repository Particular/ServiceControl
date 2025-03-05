namespace ServiceControlInstaller.Engine.UnitTests.Setup;

using Engine.Setup;
using NUnit.Framework;

[TestFixture]
public class SetupInstanceTests
{
    [Test]
    public void Should_not_throw_on_0_exit_code() => Assert.DoesNotThrow(() => InstanceSetup.Run("", "", "test", false));
}