using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TestingTool.Contracts;

namespace TestingTool.Jobs;

/// <summary>
/// Manages the lifecycle of all UI-controllable jobs (retry, archive, search): start, stop, and
/// status snapshots. This is the job counterpart of <see cref="ScenarioRunner"/> — it exposes the
/// recoverability/search jobs through the <c>/api/jobs</c> endpoints so they can be controlled
/// from the web UI instead of running as hidden config-gated background timers.
/// </summary>
public sealed class JobRunner
{
    private readonly ConcurrentDictionary<string, JobBase> _jobs;
    private readonly ILogger<JobRunner> _logger;

    public JobRunner(IEnumerable<JobBase> jobs, ILogger<JobRunner> logger)
    {
        _logger = logger;
        _jobs = new ConcurrentDictionary<string, JobBase>(
            jobs.ToDictionary(j => j.Name, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>All registered jobs.</summary>
    public IReadOnlyList<JobBase> All => _jobs.Values.ToArray();

    /// <summary>Starts a job, optionally overriding its cycle interval.</summary>
    public bool TryStart(string name, TimeSpan? interval, out string? error)
    {
        if (!_jobs.TryGetValue(name, out var job))
        {
            error = $"Unknown job '{name}'";
            return false;
        }

        var effectiveInterval = interval ?? job.DefaultInterval;
        if (!job.TryStart(effectiveInterval, out error))
            return false;

        _logger.LogInformation("Started job {Job} — interval {Interval}", name, effectiveInterval);
        return true;
    }

    /// <summary>Stops a running job.</summary>
    public bool TryStop(string name)
    {
        if (!_jobs.TryGetValue(name, out var job))
            return false;

        if (!job.IsRunning)
            return false;

        job.Stop();
        _logger.LogInformation("Stopped job {Job} after {Cycles} cycles ({Items} items processed)",
            name, job.Cycles, job.ItemsProcessed);
        return true;
    }

    /// <summary>Stops all running jobs (used on shutdown).</summary>
    public void StopAll()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.IsRunning)
                job.Stop();
        }
    }

    /// <summary>Returns a snapshot of every job's state for the API/UI.</summary>
    public List<JobInfo> GetSnapshot() =>
        _jobs.Values.OrderBy(j => j.Category).ThenBy(j => j.Name)
            .Select(j => new JobInfo
            {
                Name = j.Name,
                Description = j.Description,
                Category = j.Category,
                Running = j.IsRunning,
                IntervalSeconds = j.IsRunning ? (int)j.Interval.TotalSeconds : 0,
                DefaultIntervalSeconds = (int)j.DefaultInterval.TotalSeconds,
                Cycles = j.Cycles,
                ItemsProcessed = j.ItemsProcessed,
                StartedAt = j.StartedAt?.ToString("O")
            })
            .ToList();
}