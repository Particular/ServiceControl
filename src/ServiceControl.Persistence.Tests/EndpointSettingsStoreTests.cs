namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence;

class EndpointSettingsStoreTests : PersistenceTestBase
{
    [Test, CancelAfter(30_000)]
    public async Task UpdateEndpointSettings_stores_and_updates_existing_setting(CancellationToken cancellationToken = default)
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = false }, cancellationToken);
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true }, cancellationToken);

        var settings = await GetAllEndpointSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Has.Count.EqualTo(1));
            Assert.That(settings.Single().Name, Is.EqualTo("Sales"));
            Assert.That(settings.Single().TrackInstances, Is.True);
        }
    }

    [Test, CancelAfter(30_000)]
    public async Task Delete_removes_only_target_setting(CancellationToken cancellationToken = default)
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = false }, cancellationToken);
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Shipping", TrackInstances = true }, cancellationToken);

        await EndpointSettingsStore.Delete("Sales", cancellationToken);

        var settings = await GetAllEndpointSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Has.Count.EqualTo(1));
            Assert.That(settings.Single().Name, Is.EqualTo("Shipping"));
            Assert.That(settings.Single().TrackInstances, Is.True);
        }
    }

    async Task<IReadOnlyList<EndpointSettings>> GetAllEndpointSettings() => await EndpointSettingsStore.GetAllEndpointSettings().ToListAsync();
}
