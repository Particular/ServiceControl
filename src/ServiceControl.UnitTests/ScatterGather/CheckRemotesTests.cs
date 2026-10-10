namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Operations;

/// <summary>
/// The remotes health check probes each remote on its own budget: a remote that does not answer the probe is
/// disabled until it answers again, and neither the query time limit nor a shutdown decides that.
/// </summary>
[TestFixture]
class CheckRemotesTests
{
    const string RemoteAddress = "http://audit-1/api";

    [Test]
    public async Task A_remote_that_does_not_answer_the_probe_in_time_is_disabled_and_fails_the_check()
    {
        var settings = Settings();
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Hanging);

        var check = new CheckRemotes(settings, factory, probeTimeout: TimeSpan.FromMilliseconds(200));

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True, "a hanging remote must fail the check");
        Assert.That(result.FailureReason, Does.Contain("did not respond"));
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.True, "a hanging remote must be skipped by the queries until it answers again");
    }

    [Test]
    public async Task The_probe_has_its_own_budget_and_is_not_bound_by_the_query_time_limit()
    {
        var settings = Settings();
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], AnsweringAfter(TimeSpan.FromMilliseconds(300)), queryTimeLimit: TimeSpan.FromMilliseconds(50));

        var check = new CheckRemotes(settings, factory, probeTimeout: TimeSpan.FromSeconds(5));

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.False, "a short query time limit is not a verdict on the remote's health");
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.False);
    }

    [Test]
    public async Task A_probe_aborted_by_shutdown_does_not_disable_the_remote()
    {
        var settings = Settings();
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Hanging);

        var check = new CheckRemotes(settings, factory, probeTimeout: TimeSpan.FromSeconds(10));
        using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await check.PerformCheck(shutdown.Token);

        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "shutdown must abort the probe instead of waiting out its budget");
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.False, "an aborted probe says nothing about the remote's health");
    }

    [Test]
    public async Task A_remote_that_refuses_the_connection_is_disabled_and_fails_the_check()
    {
        var settings = Settings();
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], (_, _) => throw new HttpRequestException("Connection refused"));

        var result = await new CheckRemotes(settings, factory, probeTimeout: TimeSpan.FromSeconds(5)).PerformCheck();

        Assert.That(result.HasFailed, Is.True);
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.True);
    }

    [Test]
    public async Task A_remote_that_answers_the_probe_is_enabled_again()
    {
        var settings = Settings();
        settings.RemoteInstances[0].TemporarilyUnavailable = true;
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], AnsweringAfter(TimeSpan.Zero));

        var result = await new CheckRemotes(settings, factory, probeTimeout: TimeSpan.FromSeconds(5)).PerformCheck();

        Assert.That(result.HasFailed, Is.False);
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.False);
    }

    static Settings Settings() => new() { RemoteInstances = [new RemoteInstanceSetting(RemoteAddress)] };

    static readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Hanging = async (_, token) =>
    {
        await Task.Delay(Timeout.Infinite, token);
        return null;
    };

    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> AnsweringAfter(TimeSpan delay) => async (_, token) =>
    {
        await Task.Delay(delay, token);
        return new HttpResponseMessage(HttpStatusCode.OK);
    };
}
