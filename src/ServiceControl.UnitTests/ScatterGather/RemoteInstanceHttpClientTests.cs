namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Hosting;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.Api;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Monitoring.HeartbeatMonitoring;
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

    [Test]
    public async Task Configuration_requests_preserve_virtual_directory_prefixes()
    {
        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting("https://audit/servicecontrol/api")] };
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"host":{"instance_name":"Audit"}}""")
        }));
        var services = new ServiceCollection();
        services.AddSingleton<PersistenceSettings>(new TestPersistenceSettings());
        services.AddRemoteInstancesHttpClients(settings);
        services.AddHttpClient(settings.RemoteInstances[0].InstanceId).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var api = CreateApi(settings, provider.GetRequiredService<IHttpClientFactory>());

        var remotes = await api.GetRemoteConfigs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://audit/servicecontrol/api/configuration")));
            Assert.That(remotes[0].Status, Is.EqualTo("online"));
        }
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task Failed_http_responses_cannot_be_reported_as_online(HttpStatusCode statusCode)
    {
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("""{"host":{"instance_name":"Audit"}}""")
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://audit/") };

        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting("https://audit")] };
        var remotes = await CreateApi(settings, new StubClientFactory(client)).GetRemoteConfigs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remotes[0].Status, Is.Not.EqualTo("online"));
            Assert.That(remotes[0].Configuration, Is.Null);
        }
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{broken}")]
    public async Task Malformed_or_missing_configuration_is_not_online(string body)
    {
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://audit/") };

        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting("https://audit")] };
        var remotes = await CreateApi(settings, new StubClientFactory(client)).GetRemoteConfigs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remotes[0].Status, Is.EqualTo("error"));
            Assert.That(remotes[0].Configuration, Is.Null);
        }
    }

    [Test]
    public async Task A_timeout_returns_an_unavailable_remote_but_caller_cancellation_propagates()
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException())))
        { BaseAddress = new Uri("https://audit/") };
        var api = CreateApi(new Settings { RemoteInstances = [new RemoteInstanceSetting("https://audit")] }, new StubClientFactory(client));

        Assert.That((await api.GetRemoteConfigs())[0].Status, Is.EqualTo("unavailable"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(async () => await api.GetRemoteConfigs(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    static ConfigurationApi CreateApi(Settings settings, IHttpClientFactory clientFactory) =>
        new(null, settings, clientFactory, new MassTransitConnectorHeartbeatStatus(), new HostInformation(Guid.Empty, "localhost"));

    class StubClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public Uri RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            RequestUri = request.RequestUri;
            return respond(cancellationToken);
        }
    }

    class TestPersistenceSettings : PersistenceSettings;
}
