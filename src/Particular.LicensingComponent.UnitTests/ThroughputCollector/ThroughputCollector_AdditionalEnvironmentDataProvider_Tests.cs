namespace Particular.LicensingComponent.UnitTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_AdditionalEnvironmentDataProvider_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = services => services.AddSingleton<IEnvironmentDataProvider, TestAdditionalEnvironmentDataProvider>();

        return base.Setup();
    }

    [Test]
    public async Task Should_include_additional_environment_data_in_throughput_report()
    {
        // Arrange
        // Act
        var report = await ThroughputCollector.GenerateThroughputReport(null, null, default);
        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey("TestKey"));
        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData["TestKey"], Is.EqualTo("TestValue"));
    }

    class TestAdditionalEnvironmentDataProvider : IEnvironmentDataProvider
    {
        public IEnumerable<(string key, string value)> GetData()
        {
            yield return ("TestKey", "TestValue");
        }
    }
}
