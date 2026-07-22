namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence;

class EndpointSettingsStoreTests : PersistenceTestBase
{
    [Test]
    public async Task UpdateEndpointSettings_stores_and_updates_existing_setting()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = false }, default);
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true }, default);

        var settings = await GetAllEndpointSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Has.Count.EqualTo(1));
            Assert.That(settings.Single().Name, Is.EqualTo("Sales"));
            Assert.That(settings.Single().TrackInstances, Is.True);
        }
    }

    [Test]
    public async Task Delete_removes_only_target_setting()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = false }, default);
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Shipping", TrackInstances = true }, default);

        await EndpointSettingsStore.Delete("Sales", default);

        var settings = await GetAllEndpointSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Has.Count.EqualTo(1));
            Assert.That(settings.Single().Name, Is.EqualTo("Shipping"));
            Assert.That(settings.Single().TrackInstances, Is.True);
        }
    }

    async Task<IReadOnlyList<EndpointSettings>> GetAllEndpointSettings()
    {
        var settings = new List<EndpointSettings>();
        await foreach (var setting in EndpointSettingsStore.GetAllEndpointSettings())
        {
            settings.Add(setting);
        }

        return settings;
    }
}
