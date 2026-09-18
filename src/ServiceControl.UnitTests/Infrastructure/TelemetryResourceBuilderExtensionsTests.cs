namespace ServiceControl.UnitTests.Infrastructure;

using NUnit.Framework;
using ServiceControl.Infrastructure;

[TestFixture]
public class TelemetryResourceBuilderExtensionsTests
{
    [TestCase("service.instance.id=worker-1")]
    [TestCase("deployment.environment.name=production,service.instance.id=worker-1")]
    [TestCase("service.instance.id=worker-1,deployment.environment.name=production")]
    [TestCase(" service.instance.id = worker-1 ")]
    [TestCase("service.instance.id=")]
    public void Finds_a_declared_attribute(string resourceAttributes) =>
        Assert.That(TelemetryResourceBuilderExtensions.Declares(resourceAttributes, "service.instance.id"), Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("deployment.environment.name=production")]
    [TestCase("service.instance.identifier=worker-1")]
    [TestCase("my.service.instance.id=worker-1")]
    [TestCase("=service.instance.id")]
    public void Leaves_an_undeclared_attribute_to_the_instance(string resourceAttributes) =>
        Assert.That(TelemetryResourceBuilderExtensions.Declares(resourceAttributes, "service.instance.id"), Is.False);

    [Test]
    public void Tells_the_declared_attributes_apart()
    {
        const string resourceAttributes = "host.name=build-agent-3,process.pid=1234";

        Assert.Multiple(() =>
        {
            Assert.That(TelemetryResourceBuilderExtensions.Declares(resourceAttributes, "host.name"), Is.True);
            Assert.That(TelemetryResourceBuilderExtensions.Declares(resourceAttributes, "process.pid"), Is.True);
            Assert.That(TelemetryResourceBuilderExtensions.Declares(resourceAttributes, "service.instance.id"), Is.False);
        });
    }
}
