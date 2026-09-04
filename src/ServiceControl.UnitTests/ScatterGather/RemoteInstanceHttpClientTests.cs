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
    public void The_remote_client_waits_out_the_query_time_limit_plus_a_margin_for_the_answer()
    {
        // A remote's query is allowed the same query time as ours, and its 504 still has to travel back.
        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting("http://audit/api")] };

        var services = new ServiceCollection();
        services.AddSingleton<PersistenceSettings>(new TestPersistenceSettings { QueryTimeout = TimeSpan.FromMinutes(5) });
        services.AddRemoteInstancesHttpClients(settings);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(settings.RemoteInstances[0].InstanceId);

        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5) + RemoteInstanceServiceCollectionExtensions.ResponseMargin));
    }

    class TestPersistenceSettings : PersistenceSettings;
}
