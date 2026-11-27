namespace TestingTool.Smoke;

/// <summary>Stops a running smoke run on shutdown and waits for its cleanup to finish.</summary>
public sealed class SmokeRunnerLifecycle(SmokeRunner runner) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        runner.Stop();
        await runner.WaitForCompletionAsync().WaitAsync(cancellationToken);
    }
}

/// <summary>The jobs that act on every failed message group, so they must not run together with a smoke run.</summary>
public static class SmokeJobConflicts
{
    static readonly HashSet<string> Names = new(["retry", "archive", "retention-sweep"], StringComparer.OrdinalIgnoreCase);

    public static bool Conflicts(string jobName) => Names.Contains(jobName);
}
