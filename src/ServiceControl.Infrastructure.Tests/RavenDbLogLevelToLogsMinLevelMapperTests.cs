namespace ServiceControl.Infrastructure.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
public class RavenDbLogLevelToLogsMinLevelMapperTests
{
    [TestCase("Trace", "Trace")]
    [TestCase("Debug", "Debug")]
    [TestCase("Info", "Info")]
    [TestCase("Warn", "Warn")]
    [TestCase("Error", "Error")]
    [TestCase("Fatal", "Fatal")]
    [TestCase("Off", "Off")]
    [TestCase("warn", "Warn")]
    [TestCase("OFF", "Off")]
    [TestCase("None", "Off")]
    [TestCase("Operations", "Info")]
    [TestCase("Information", "Debug")]
    [TestCase("operations", "Info")]
    public void Should_map_the_configured_level_to_the_server_minimum_level(string configured, string expected) =>
        Assert.That(RavenDbLogLevelToLogsMinLevelMapper.Map(configured, NullLogger.Instance), Is.EqualTo(expected));

    [Test]
    public void Should_fall_back_to_Warn_for_an_unknown_level() =>
        Assert.That(RavenDbLogLevelToLogsMinLevelMapper.Map("Verbose", NullLogger.Instance), Is.EqualTo("Warn"));
}
