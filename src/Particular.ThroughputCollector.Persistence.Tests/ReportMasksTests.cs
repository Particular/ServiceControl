namespace Particular.ThroughputCollector.Persistence.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class ReportMasksTests : PersistenceTestFixture
{
    [Test]
    public async Task Should_retrieve_saved_report_masks()
    {
        //Arrange
        var expectedReportMasks = new List<string> { "secret", "boo" };
        await DataStore.SaveReportMasks(expectedReportMasks, default);

        //Act
        var retrievedReportMasks = await DataStore.GetReportMasks(default);

        //Assert
        Assert.That(retrievedReportMasks, Is.Not.Null);
        Assert.That(retrievedReportMasks, Is.EquivalentTo(expectedReportMasks));
    }

    [Test]
    public async Task Should_update_existing_report_masks_if_already_exists()
    {
        // Arrange
        var oldReportMasks = new List<string> { "secret", "boo" };
        await DataStore.SaveReportMasks(oldReportMasks, default);

        // Act
        var expectedReportMasks = new List<string> { "secret", "hello" };
        await DataStore.SaveReportMasks(expectedReportMasks, default);
        var retrievedReportMasks = await DataStore.GetReportMasks(default);

        // Assert
        Assert.That(retrievedReportMasks, Is.Not.Null);
        Assert.That(retrievedReportMasks, Is.EquivalentTo(expectedReportMasks));
    }
}