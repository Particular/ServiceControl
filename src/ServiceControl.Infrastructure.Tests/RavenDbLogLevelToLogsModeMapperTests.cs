namespace ServiceControl.Infrastructure.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
public class RavenDbLogLevelToLogsModeMapperTests
{
    [TestCase("None", "None")]
    [TestCase("Off", "None")]
    [TestCase("off", "None")]
    [TestCase("Information", "Information")]
    [TestCase("Info", "Information")]
    [TestCase("Debug", "Information")]
    [TestCase("Trace", "Information")]
    [TestCase("Operations", "Operations")]
    [TestCase("operations", "Operations")]
    [TestCase("Warn", "Operations")]
    [TestCase("Error", "Operations")]
    [TestCase("Fatal", "Operations")]
    public void Should_map_the_configured_level_to_the_server_log_mode(string configured, string expected) =>
        Assert.That(RavenDbLogLevelToLogsModeMapper.Map(configured, NullLogger.Instance), Is.EqualTo(expected));

    [Test]
    public void Should_fall_back_to_Operations_for_an_unknown_level() =>
        Assert.That(RavenDbLogLevelToLogsModeMapper.Map("Verbose", NullLogger.Instance), Is.EqualTo("Operations"));
}
