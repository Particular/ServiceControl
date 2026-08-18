namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class LauncherShutdownTimeoutTests
{
    [Test]
    public void Missing_value_uses_twenty_second_default() =>
        Assert.That(LauncherShutdownTimeout.Parse(null), Is.EqualTo(TimeSpan.FromSeconds(20)));

    [TestCase("2.5s", 2.5)]
    [TestCase("00:00:03", 3)]
    public void Positive_durations_are_accepted(string value, double seconds) =>
        Assert.That(LauncherShutdownTimeout.Parse(value), Is.EqualTo(TimeSpan.FromSeconds(seconds)));

    [TestCase("")]
    [TestCase("never")]
    [TestCase("0s")]
    [TestCase("-1s")]
    public void Invalid_or_non_positive_durations_are_rejected(string value)
    {
        var exception = Assert.Throws<LauncherConfigurationException>(() => LauncherShutdownTimeout.Parse(value));

        Assert.That(exception!.Message, Does.Contain(LauncherShutdownTimeout.EnvironmentVariable));
    }
}