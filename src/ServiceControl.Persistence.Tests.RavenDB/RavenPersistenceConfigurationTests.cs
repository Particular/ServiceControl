namespace ServiceControl.Persistence.Tests;

using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;

[TestFixture]
class RavenPersistenceConfigurationTests
{
    [Test]
    public void Should_reject_maintenance_mode_with_external_server()
    {
        var settings = new RavenPersisterSettings { MaintenanceMode = true, ConnectionString = "http://someserver:44444" };

        var exception = Assert.Throws<InvalidOperationException>(() => new RavenPersistenceConfiguration().Create(settings));

        Assert.That(exception.Message, Does.Contain("Maintenance mode is not supported"));
    }

    [Test]
    public void Should_allow_maintenance_mode_with_embedded_server()
    {
        var settings = new RavenPersisterSettings { MaintenanceMode = true, DatabasePath = "c://db-path" };

        var services = new ServiceCollection();
        new RavenPersistenceConfiguration().Create(settings).AddPersistence(services);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.Not.Null);
    }

    [Test]
    public void Should_allow_normal_mode_with_external_server()
    {
        var settings = new RavenPersisterSettings { ConnectionString = "http://someserver:44444" };

        Assert.That(new RavenPersistenceConfiguration().Create(settings), Is.Not.Null);
    }
}
