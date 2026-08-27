namespace ServiceControl.UnitTests.Licensing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ServiceControl;

[TestFixture]
class HostEnvironmentDataProviderTests
{
    [SetUp]
    public async Task SetUp()
    {
        data = [];

        foreach (var datum in new HostEnvironmentDataProvider().GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }
    }

    [Test]
    public void Should_report_a_known_hosting_model() =>
        Assert.That(data["Host.Model"], Is.AnyOf("Container", "WindowsService", "Console"));

    [Test]
    public void Should_report_whether_kubernetes_is_orchestrating() =>
        Assert.That(data["Host.Orchestrator"], Is.AnyOf("Kubernetes", "None"));

    [Test]
    public void Should_report_a_known_os_platform() =>
        Assert.That(data["Host.OSPlatform"], Is.AnyOf("Windows", "Linux", "macOS", "Unknown"));

    [Test]
    public void Should_report_os_version_as_major_and_minor_only() =>
        Assert.That(data["Host.OSVersion"], Does.Match(@"^\d+\.\d+$"));

    [Test]
    public void Should_report_runtime_version_without_build_metadata() =>
        Assert.That(data["Host.RuntimeVersion"], Does.Match(@"^\d+\.\d+\.\d+$"));

    [Test]
    public void Should_report_a_positive_processor_count() =>
        Assert.That(int.Parse(data["Host.ProcessorCount"]), Is.GreaterThan(0));

    [Test]
    public void Should_report_available_memory_in_whole_gigabytes() =>
        Assert.That(data["Host.AvailableMemoryGB"], Does.Match(@"^\d+$").Or.EqualTo("Unknown"));

    [Test]
    public void Should_not_report_any_value_that_could_identify_the_machine()
    {
        var machineIdentifiers = new[]
        {
            Environment.MachineName,
            Environment.UserName,
            RuntimeInformation.OSDescription
        };

        foreach (var value in data.Values)
        {
            Assert.That(machineIdentifiers, Has.None.EqualTo(value), $"Value '{value}' identifies the machine");
        }
    }

    Dictionary<string, string> data;
}
