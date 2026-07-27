namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.CustomChecks;
using NUnit.Framework;

class CustomCheckLogicTests : PersistenceTestBase
{
    [Test]
    public async Task CheckFreeDiskSpace_performs_check()
    {
        var check = ServiceProvider.GetServices<ICustomCheck>()
            .Single(c => c.Id == "ServiceControl database" && c.Category == "Storage space");

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.False);
    }

    [Test]
    public async Task CheckMinimumStorageRequiredForIngestion_performs_check()
    {
        var check = ServiceProvider.GetServices<ICustomCheck>()
            .Single(c => c.Id == "Message Ingestion Process" && c.Category == "ServiceControl Health");

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.False);
    }

    [Test]
    public async Task CheckMinimumStorageRequiredForIngestion_allows_ingestion_when_check_passes()
    {
        var state = ServiceProvider.GetRequiredService<MinimumRequiredStorageState>();

        var check = ServiceProvider.GetServices<ICustomCheck>()
            .Single(c => c.Id == "Message Ingestion Process" && c.Category == "ServiceControl Health");

        await check.PerformCheck();

        Assert.That(state.CanIngestMore, Is.True);
    }
}
