namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Persistence;

[TestFixture]
class RemoteInstanceHttpClientTests
{
    [Test]
    public void The_remote_client_waits_no_longer_than_the_query_time_limit()
    {
        // This instance's limit bounds the whole composite: a remote that is slow, hangs or has a laxer limit
        // of its own is reported as missing once ours runs out, whatever its own configuration says.
        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting("http://audit/api")] };

        var services = new ServiceCollection();
        services.AddSingleton<PersistenceSettings>(new TestPersistenceSettings { QueryTimeout = TimeSpan.FromMinutes(5) });
        services.AddRemoteInstancesHttpClients(settings);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(settings.RemoteInstances[0].InstanceId);

        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    class TestPersistenceSettings : PersistenceSettings;
}
