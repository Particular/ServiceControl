using System.Text.RegularExpressions;

namespace TestingTool;

/// <summary>
/// Resolves the shard id for this replica. When scaled horizontally, each replica must own a
/// disjoint slice of the scenario space so that deterministic failure decisions (see
/// <see cref="Scenarios.ScenarioBase.Hash"/>) don't overlap across pods.
///
/// Resolution order:
/// 1. <c>SHARD_ID</c> environment variable (explicit override — used by docker-compose and
///    manual runs).
/// 2. Ordinal extracted from a Kubernetes StatefulSet hostname (e.g. <c>testing-tool-2</c>
///    → <c>2</c>). StatefulSets give stable, ordered pod names so shards are deterministic
///    across restarts.
/// 3. <see cref="Environment.MachineName"/> — unique per pod for Deployments, deterministic
///    per host for bare-metal/VM runs.
/// </summary>
public static class ShardIdResolver
{
    // Matches a trailing -<number> at the end of a hostname (StatefulSet pod naming convention).
    private static readonly Regex StatefulSetOrdinal = new(@"-(\d+)$", RegexOptions.Compiled);

    /// <summary>Resolves the shard id for this replica.</summary>
    public static string Resolve()
    {
        var explicitId = Environment.GetEnvironmentVariable("SHARD_ID");
        if (!string.IsNullOrWhiteSpace(explicitId))
            return explicitId;

        var hostname = Environment.MachineName;
        var match = StatefulSetOrdinal.Match(hostname);
        if (match.Success)
            return match.Groups[1].Value;

        return hostname;
    }
}