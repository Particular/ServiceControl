namespace ServiceControlInstaller.Engine.UnitTests.Setup;

using System;
using System.Threading;
using Engine.Setup;
using NUnit.Framework;

[TestFixture]
public class SetupInstanceTests
{
    [Test]
    public void Should_not_throw_on_0_exit_code() => Assert.DoesNotThrow(() => InstanceSetup.Run(TestContext.CurrentContext.WorkDirectory, "SetupProcessFake.exe", "test", ""));

    [Test]
    public void Should_capture_and_rethrow_failures()
    {
        var ex = Assert.Throws<Exception>(() => InstanceSetup.Run(TestContext.CurrentContext.WorkDirectory, "SetupProcessFake.exe", "test", "fail"));

        Assert.That(ex.Message, Does.Contain("Fake exception"));
    }

    [Test]
    public void Should_capture_and_rethrow_non_zero_exit_codes()
    {
        var ex = Assert.Throws<Exception>(() => InstanceSetup.Run(TestContext.CurrentContext.WorkDirectory, "SetupProcessFake.exe", "test", "non-zero-exit-code"));

        Assert.That(ex.Message, Does.Contain("returned a non-zero exit code: 3"));
        Assert.That(ex.Message, Does.Contain("Fake non zero exit code message"));
    }
}