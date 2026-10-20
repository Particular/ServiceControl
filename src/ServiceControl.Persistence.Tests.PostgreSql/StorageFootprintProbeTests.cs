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
            Assert.That(footprint.MessageCount ?? 0, Is.EqualTo(0), "A never analysed table has no row estimate, which reads back as null");
        }
    }

    [Test]
    public async Task Editions_do_not_apply()
    {
        var probe = ServiceProvider.GetRequiredService<IDatabaseHostingProbe>();

        var hosting = await probe.Probe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hosting.ServerEdition, Is.EqualTo(DatabaseHosting.NotApplicable));
            Assert.That(hosting.ServiceObjective, Is.EqualTo(DatabaseHosting.NotApplicable));
        }
    }
}
