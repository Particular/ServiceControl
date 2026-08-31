namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_EnvironmentDataFailure_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = services =>
        {
            services.AddSingleton<IEnvironmentDataProvider, ProviderWithOneUnreadableDatum>();
            services.AddSingleton<IEnvironmentDataProvider, ProviderThatCannotListItsData>();
        };

        return base.Setup();
    }

    [Test]
    public async Task Should_keep_the_siblings_of_a_datum_that_cannot_be_read()
    {
        var report = await ThroughputCollector.GenerateThroughputReport(null, null);

        var environmentData = report.ReportData.EnvironmentInformation.EnvironmentData;

        Assert.Multiple(() =>
        {
            Assert.That(environmentData["Readable.Before"], Is.EqualTo("value"),
                "A datum listed before the failing one has already been read");
            Assert.That(environmentData["Readable.After"], Is.EqualTo("value"),
                "A datum listed after the failing one must still be read, unlike an iterator that has faulted");
            Assert.That(environmentData["Unreadable"], Is.EqualTo("ReadFailed"),
                "The failure is recorded rather than leaving the key absent");
        });
    }

    [Test]
    public async Task Should_still_report_when_a_provider_cannot_list_its_data()
    {
        var report = await ThroughputCollector.GenerateThroughputReport(null, null);

        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Does.ContainKey("Readable.Before"),
            "One broken provider must not cost another provider its data");
    }

    class ProviderWithOneUnreadableDatum : IEnvironmentDataProvider
    {
        public IEnumerable<EnvironmentDatum> GetData() =>
        [
            EnvironmentDatum.Value("Readable.Before", () => "value"),
            EnvironmentDatum.Deferred("Unreadable", _ => throw new InvalidOperationException("the storage read failed")),
            EnvironmentDatum.Value("Readable.After", () => "value")
        ];
    }

    class ProviderThatCannotListItsData : IEnvironmentDataProvider
    {
        public IEnumerable<EnvironmentDatum> GetData() => throw new InvalidOperationException("the provider is broken");
    }
}
