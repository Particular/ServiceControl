namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

class StorageFootprintProbeTests : PersistenceTestBase
{
    [Test]
    public async Task Measures_schema_size_and_message_rows()
    {
        var probe = ServiceProvider.GetRequiredService<IStorageFootprintProbe>();

        var footprint = await probe.Probe();

        Assert.That(footprint, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(footprint.SizeGB, Is.GreaterThan(0));
            Assert.That(footprint.MessageCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Reports_the_engine_edition()
    {
        var probe = ServiceProvider.GetRequiredService<IDatabaseHostingProbe>();

        var hosting = await probe.Probe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hosting.ServerEdition, Is.Not.EqualTo(DatabaseHosting.NotApplicable));
            Assert.That(hosting.ServiceObjective, Is.EqualTo(DatabaseHosting.NotApplicable));
        }
    }
}
