namespace ServiceControl.Persistence.Tests.Throughput;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class ReportMasksTests : PersistenceTestBase
{
    [Test]
    public async Task Should_retrieve_saved_report_masks()
    {
        //Arrange
        var expectedReportMasks = new List<string> { "secret", "boo" };
        await LicensingDataStore.SaveReportMasks(expectedReportMasks, default);

        //Act
        var retrievedReportMasks = await LicensingDataStore.GetReportMasks(default);

        //Assert
        Assert.That(retrievedReportMasks, Is.Not.Null);
        Assert.That(retrievedReportMasks, Is.EquivalentTo(expectedReportMasks));
    }

    [Test]
    public async Task Should_update_existing_report_masks_if_already_exists()
    {
        // Arrange
        var oldReportMasks = new List<string> { "secret", "boo" };
        await LicensingDataStore.SaveReportMasks(oldReportMasks, default);

        // Act
        var expectedReportMasks = new List<string> { "secret", "hello" };
        await LicensingDataStore.SaveReportMasks(expectedReportMasks, default);
        var retrievedReportMasks = await LicensingDataStore.GetReportMasks(default);

        // Assert
        Assert.That(retrievedReportMasks, Is.Not.Null);
        Assert.That(retrievedReportMasks, Is.EquivalentTo(expectedReportMasks));
    }
}