using System.Text.Json;
using NServiceBus;
using NServiceBus.Features;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TestingTool.Smoke;

namespace TestingTool.Tests.Smoke;

/// <summary>Runs real in-process endpoints on the Learning transport in a temporary folder. No broker is needed.</summary>
[NonParallelizable]
public class SmokeEndpointLifecycleTests
{
    string storage = null!;
    SmokeObservations observations = null!;

    [SetUp]
    public void SetUp()
    {
        storage = Path.Combine(Path.GetTempPath(), "smoke-lifecycle-" + Guid.NewGuid().ToString("N"));
        observations = new SmokeObservations();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(storage))
        {
            Directory.Delete(storage, recursive: true);
        }
    }

    Task<SmokeEndpoint> Start(string name, Action<EndpointConfiguration>? configure = null, CancellationToken ct = default) =>
        SmokeEndpoint.Start(name, config => config.UseTransport(new LearningTransport { StorageDirectory = storage }), configure, observations, "error", ct);

    static Dictionary<string, string> Headers(string metadataFile) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(metadataFile))!;

    IEnumerable<Dictionary<string, string>> QueueHeaders(string queue) =>
        Directory.Exists(Path.Combine(storage, queue)) ? Directory.GetFiles(Path.Combine(storage, queue), "*.metadata.txt").Select(Headers) : [];

    static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for the condition");
            await Task.Delay(50);
        }
    }

    [Test]
    public async Task Two_endpoints_handle_messages_with_their_own_identity_and_can_be_recreated()
    {
        await using var source = await Start("Smoke.Source");
        await using var target = await Start("Smoke.Target");
        Assert.That(source.MainAddress, Is.Not.EqualTo(target.MainAddress));

        var send = new SendOptions();
        send.SetDestination(target.MainAddress);
        await source.Session.Send(new SmokeMessage { Marker = "to-target" }, send);
        await WaitUntil(() => observations.FindHandled("to-target") is not null);
        Assert.That(observations.FindHandled("to-target")!.Endpoint, Is.EqualTo("Smoke.Target"));

        await target.StopAsync(CancellationToken.None);
        await target.StopAsync(CancellationToken.None);

        await using var recreated = await Start("Smoke.Target");
        var again = new SendOptions();
        again.SetDestination(recreated.MainAddress);
        await source.Session.Send(new SmokeMessage { Marker = "to-recreated" }, again);
        await WaitUntil(() => observations.FindHandled("to-recreated") is not null);
        Assert.That(recreated.MainAddress, Is.EqualTo(target.MainAddress));
    }

    [Test]
    public async Task A_failing_message_reaches_the_error_queue_from_the_endpoint_that_failed_it()
    {
        await using var source = await Start("Smoke.Source");
        var send = new SendOptions();
        send.SetDestination(source.MainAddress);
        await source.Session.Send(new SmokeMessage { Marker = "fails", Fail = true, FailureType = "TestingTool.Smoke.Failure_run_check" }, send);

        await WaitUntil(() => QueueHeaders("error").Any());
        var headers = QueueHeaders("error").Single();
        Assert.That(headers["NServiceBus.FailedQ"], Is.EqualTo(source.MainAddress));
        Assert.That(headers["NServiceBus.ExceptionInfo.ExceptionType"], Is.EqualTo("TestingTool.Smoke.Failure_run_check"));
        Assert.That(observations.FindHandled("fails"), Is.Null);
    }

    [Test]
    public async Task The_heartbeat_plugin_sends_heartbeats_to_the_service_control_queue()
    {
        Directory.CreateDirectory(Path.Combine(storage, "Particular.ServiceControl"));

        await using var endpoint = await Start("Smoke.Heartbeat", config => config.SendHeartbeatTo("Particular.ServiceControl", TimeSpan.FromSeconds(1)));

        await WaitUntil(() => QueueHeaders("Particular.ServiceControl").Any(h => h["NServiceBus.EnclosedMessageTypes"] == "ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat"));
    }

    [Test]
    public async Task The_metrics_plugin_reports_processing_time_of_handled_messages_with_scanning_disabled()
    {
        Directory.CreateDirectory(Path.Combine(storage, "Particular.Monitoring"));

        await using var endpoint = await Start("Smoke.Metrics", config => config.EnableMetrics().SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromSeconds(1)));
        var send = new SendOptions();
        send.SetDestination(endpoint.MainAddress);
        for (var i = 0; i < 3; i++)
        {
            await endpoint.Session.Send(new SmokeMessage { Marker = $"metrics-{i}", DelayMilliseconds = 100 }, send);
        }

        await WaitUntil(() => Enumerable.Range(0, 3).All(i => observations.FindHandled($"metrics-{i}")?.Endpoint == endpoint.Name));
        await WaitUntil(() => QueueHeaders("Particular.Monitoring").Any(h => h.GetValueOrDefault("NServiceBus.Metric.Type") == "ProcessingTime"));
    }

    [Test]
    public async Task A_host_that_fails_to_start_is_disposed_and_a_later_start_works()
    {
        var blockedStorage = Path.Combine(Path.GetTempPath(), "smoke-blocked-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blockedStorage, "a file where the storage folder should be");
        try
        {
            var ex = Assert.ThrowsAsync<SmokeCheckFailedException>(() => SmokeEndpoint.Start(
                "Smoke.Broken", config => config.UseTransport(new LearningTransport { StorageDirectory = blockedStorage }), null, observations, "error", CancellationToken.None));
            Assert.That(ex!.Message, Does.Contain("Smoke.Broken could not start"));
        }
        finally
        {
            File.Delete(blockedStorage);
        }

        await using var working = await Start("Smoke.Broken");
        Assert.That(working.MainAddress, Is.Not.Empty);
    }

    [Test]
    public async Task Canceling_while_the_host_is_starting_keeps_the_cancellation_and_stops_the_services_that_started()
    {
        var probe = new StartupProbe();
        using var cancel = new CancellationTokenSource();
        var start = Start("Smoke.Cancelled", config =>
        {
            EarlierFeature.Probe = probe;
            config.EnableFeature<EarlierFeature>();
            config.EnableFeature<BlockingFeature>();
        }, cancel.Token);

        await WaitUntil(() => probe.Blocking);
        cancel.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await start);
        Assert.That(probe.EarlierStarted && probe.EarlierStopped, Is.True, "the service that had started was stopped when the start was canceled");
    }

    [Test]
    public void A_pre_cancelled_start_throws_cancellation()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => Start("Smoke.PreCancelled", ct: cancelled.Token));
    }

    [Test]
    public async Task The_runner_stops_endpoint_hosts_before_http_cleanup_that_never_finishes_and_disposes_them()
    {
        var checkEndpoint = default(SmokeEndpoint);
        var stopped = false;
        var cleanupSawStoppedHost = false;
        var check = new SmokeCheck("a", "a", "a", async (ctx, ct) =>
        {
            checkEndpoint = await Start("Smoke.Runner");
            ctx.OnCleanup(async token =>
            {
                // Like an API call that blocks until its time limit, registered before the host stop.
                cleanupSawStoppedHost = stopped;
                await Task.Delay(Timeout.Infinite, token);
            });
            ctx.OnEndpointStop(async token =>
            {
                await checkEndpoint.StopAsync(token);
                stopped = true;
            });
        });
        var runner = new SmokeRunner([check], () => null, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50), NullLogger<SmokeRunner>.Instance);

        Assert.That(runner.TryStart(out _), Is.True);
        await runner.WaitForCompletionAsync();

        Assert.That(cleanupSawStoppedHost, Is.True);
        var send = new SendOptions();
        send.SetDestination(checkEndpoint!.MainAddress);
        await using var other = await Start("Smoke.Other");
        await other.Session.Send(new SmokeMessage { Marker = "after-stop" }, send);
        await Task.Delay(500);
        Assert.That(observations.FindHandled("after-stop"), Is.Null, "the stopped host no longer receives messages");
    }

    sealed class StartupProbe
    {
        public volatile bool EarlierStarted;
        public volatile bool EarlierStopped;
        public volatile bool Blocking;
    }

    /// <summary>Starts and completes first, so a later cancellation leaves a started service behind.</summary>
    sealed class EarlierFeature : Feature
    {
        public static StartupProbe Probe { get; set; } = new();

        protected override void Setup(FeatureConfigurationContext context) =>
            context.RegisterStartupTask(new EarlierTask(Probe));

        sealed class EarlierTask(StartupProbe probe) : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                probe.EarlierStarted = true;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                probe.EarlierStopped = true;
                return Task.CompletedTask;
            }
        }
    }

    sealed class BlockingFeature : Feature
    {
        public BlockingFeature() => DependsOn<EarlierFeature>();

        protected override void Setup(FeatureConfigurationContext context) =>
            context.RegisterStartupTask(new BlockingTask(EarlierFeature.Probe));

        sealed class BlockingTask(StartupProbe probe) : FeatureStartupTask
        {
            protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                probe.Blocking = true;
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
