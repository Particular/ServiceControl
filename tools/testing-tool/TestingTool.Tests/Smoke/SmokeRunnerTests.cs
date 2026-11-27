using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TestingTool.Contracts;
using TestingTool.Smoke;

namespace TestingTool.Tests.Smoke;

public class SmokeRunnerTests
{
    static SmokeRunner Create(IReadOnlyList<SmokeCheck> checks, Func<string?>? conflict = null, TimeSpan? checkTimeout = null, TimeSpan? cleanupTimeout = null) =>
        new(checks, conflict ?? (() => null), checkTimeout ?? TimeSpan.FromSeconds(5), cleanupTimeout ?? TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(50), NullLogger<SmokeRunner>.Instance);

    static SmokeCheck Check(string id, Func<SmokeContext, CancellationToken, Task> run, TimeSpan? timeout = null) => new(id, id, id, run, timeout);

    static async Task<SmokeRunInfo> RunToEnd(SmokeRunner runner)
    {
        Assert.That(runner.TryStart(out var error), Is.True, error);
        await runner.WaitForCompletionAsync();
        return runner.GetSnapshot();
    }

    [Test]
    public async Task Runs_checks_in_order_and_continues_after_a_failure()
    {
        var order = new List<string>();
        var runner = Create([
            Check("a", (_, _) => { order.Add("a"); return Task.CompletedTask; }),
            Check("b", (_, _) => { order.Add("b"); throw new SmokeCheckFailedException("expected effect missing"); }),
            SmokeCheck.Manual("m", "manual", "a person checks this"),
            Check("c", (ctx, _) => { order.Add("c"); ctx.Detail = "ok"; return Task.CompletedTask; })]);

        var run = await RunToEnd(runner);

        Assert.That(order, Is.EqualTo(["a", "b", "c"]));
        Assert.That(run.Checks.Select(c => c.Status), Is.EqualTo([SmokeCheckStatus.Passed, SmokeCheckStatus.Failed, SmokeCheckStatus.Manual, SmokeCheckStatus.Passed]));
        Assert.That(run.Checks[1].Detail, Is.EqualTo("expected effect missing"));
        Assert.That(run.Checks[3].Detail, Is.EqualTo("ok"));
        Assert.That(run.Running, Is.False);
        Assert.That(run.FinishedAt, Is.Not.Null);
    }

    [Test]
    public async Task Unexpected_errors_fail_the_check_with_the_error_type()
    {
        var runner = Create([Check("a", (_, _) => throw new HttpRequestException("connection refused"))]);

        var run = await RunToEnd(runner);

        Assert.That(run.Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Failed));
        Assert.That(run.Checks[0].Detail, Is.EqualTo("HttpRequestException: connection refused"));
    }

    [Test]
    public async Task Manual_rows_are_listed_before_and_after_a_run_without_executing()
    {
        var runner = Create([SmokeCheck.Manual("m", "manual", "desc")]);
        Assert.That(runner.GetSnapshot().Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Manual));

        var run = await RunToEnd(runner);

        Assert.That(run.Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Manual));
        Assert.That(run.Checks[0].DurationSeconds, Is.Null);
    }

    [Test]
    public async Task Rejects_a_second_start_while_running_and_accepts_one_after()
    {
        var release = new TaskCompletionSource();
        var runner = Create([Check("a", async (_, _) => await release.Task)]);
        Assert.That(runner.TryStart(out _), Is.True);

        Assert.That(runner.TryStart(out var error), Is.False);
        Assert.That(error, Does.Contain("already in progress"));
        Assert.That(runner.GetSnapshot().Running, Is.True);

        release.SetResult();
        await runner.WaitForCompletionAsync();
        Assert.That(runner.TryStart(out _), Is.True);
        await runner.WaitForCompletionAsync();
    }

    [Test]
    public void Refuses_to_start_when_a_conflicting_job_is_running()
    {
        var runner = Create([Check("a", (_, _) => Task.CompletedTask)], () => "Stop the 'retry' job first.");

        Assert.That(runner.TryStart(out var error), Is.False);
        Assert.That(error, Is.EqualTo("Stop the 'retry' job first."));
        Assert.That(runner.GetSnapshot().RunId, Is.Null);
    }

    [Test]
    public async Task Stop_cancels_the_running_check_and_the_pending_ones_and_still_cleans_up()
    {
        var started = new TaskCompletionSource();
        var cleaned = false;
        var secondRan = false;
        var runner = Create([
            Check("a", async (ctx, ct) =>
            {
                ctx.OnCleanup(_ => { cleaned = true; return Task.CompletedTask; });
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }),
            Check("b", (_, _) => { secondRan = true; return Task.CompletedTask; })]);
        runner.TryStart(out _);
        await started.Task;

        runner.Stop();
        await runner.WaitForCompletionAsync();

        var run = runner.GetSnapshot();
        Assert.That(run.Checks.Select(c => c.Status), Is.EqualTo([SmokeCheckStatus.Canceled, SmokeCheckStatus.Canceled]));
        Assert.That(cleaned, Is.True);
        Assert.That(secondRan, Is.False);
    }

    [Test]
    public async Task A_check_that_exceeds_its_timeout_fails_and_the_next_check_runs()
    {
        var runner = Create([
            Check("slow", (_, ct) => Task.Delay(Timeout.Infinite, ct), TimeSpan.FromMilliseconds(30)),
            Check("next", (_, _) => Task.CompletedTask)]);

        var run = await RunToEnd(runner);

        Assert.That(run.Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Failed));
        Assert.That(run.Checks[0].Detail, Does.Contain("did not finish within"));
        Assert.That(run.Checks[1].Status, Is.EqualTo(SmokeCheckStatus.Passed));
    }

    [Test]
    public async Task Cleanups_run_in_reverse_order_after_a_failure_and_a_cleanup_error_is_reported()
    {
        var order = new List<string>();
        var runner = Create([Check("a", (ctx, _) =>
        {
            ctx.OnCleanup(_ => { order.Add("first"); return Task.CompletedTask; });
            ctx.OnCleanup(_ => throw new InvalidOperationException("could not delete"));
            throw new SmokeCheckFailedException("boom");
        })]);

        var run = await RunToEnd(runner);

        Assert.That(order, Is.EqualTo(["first"]));
        Assert.That(run.Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Failed));
        Assert.That(run.Checks[0].Detail, Does.StartWith("boom").And.Contain("could not delete"));
    }

    [Test]
    public async Task WaitFor_reports_the_last_observation_when_it_times_out()
    {
        var runner = Create([Check("a", async (ctx, ct) =>
            await ctx.WaitFor<int>("the effect", _ => Task.FromResult(PollResult.Waiting<int>("saw 2 of 3")), ct))]);

        var run = await RunToEnd(runner);

        Assert.That(run.Checks[0].Detail, Does.Contain("waiting for the effect").And.Contain("Last observed: saw 2 of 3"));
    }

    [Test]
    public async Task Each_run_gets_its_own_run_id_and_the_last_run_stays_visible()
    {
        var ids = new List<string>();
        var runner = Create([Check("a", (ctx, _) => { ids.Add(ctx.RunId); return Task.CompletedTask; })]);

        var first = await RunToEnd(runner);
        var second = await RunToEnd(runner);

        Assert.That(ids, Has.Count.EqualTo(2));
        Assert.That(ids[0], Is.Not.EqualTo(ids[1]));
        Assert.That(first.RunId, Is.EqualTo(ids[0]));
        Assert.That(second.RunId, Is.EqualTo(ids[1]));
        Assert.That(runner.GetSnapshot().Checks[0].Status, Is.EqualTo(SmokeCheckStatus.Passed));
    }

    [Test]
    public async Task A_cleanup_that_outlives_its_time_limit_is_cancelled_and_awaited_not_abandoned()
    {
        var observedCancellation = false;
        var finished = false;
        var runner = Create([Check("a", (ctx, _) =>
        {
            ctx.OnCleanup(async token =>
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { observedCancellation = true; throw; }
                finally { finished = true; }
            });
            return Task.CompletedTask;
        })], cleanupTimeout: TimeSpan.FromMilliseconds(50));

        var run = await RunToEnd(runner);

        Assert.That(observedCancellation && finished, Is.True);
        Assert.That(run.Checks[0].Detail, Does.Contain("timed out"));
    }

    [Test]
    public async Task Endpoint_stops_run_before_cleanups_even_when_a_stop_fails_and_a_cleanup_blocks()
    {
        var order = new List<string>();
        var runner = Create([Check("a", (ctx, _) =>
        {
            ctx.OnCleanup(async token =>
            {
                order.Add("blocking cleanup");
                await Task.Delay(Timeout.Infinite, token);
            });
            ctx.OnEndpointStop(_ => { order.Add("stop first"); return Task.CompletedTask; });
            ctx.OnEndpointStop(_ => { order.Add("stop second"); throw new InvalidOperationException("stop failed"); });
            return Task.CompletedTask;
        })], cleanupTimeout: TimeSpan.FromMilliseconds(50));

        var run = await RunToEnd(runner);

        Assert.That(order, Is.EqualTo(["stop second", "stop first", "blocking cleanup"]));
        Assert.That(run.Checks[0].Detail, Does.Contain("stop failed").And.Contain("timed out"));
    }
}
