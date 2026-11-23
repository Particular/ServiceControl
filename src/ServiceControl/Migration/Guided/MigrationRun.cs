namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// What the checks step found in the checkpoint table, which decides the steps that apply.
/// </summary>
enum MigrationRunState
{
    FirstRun,
    Resume,
    RequiredSettled
}

/// <summary>
/// The state the steps of one guided migration share. Each step sets what the steps after it read.
/// </summary>
sealed class MigrationRun(
    Settings settings,
    IMigrationSource source,
    IMigrationTarget target,
    IMigrationCheckpointStore checkpointStore,
    IMigrationTargetReadiness readiness,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    GuidedMigrationArguments arguments,
    MigrationConsole console)
{
    public Settings Settings { get; } = settings;

    public IMigrationSource Source { get; } = source;

    public IMigrationTarget Target { get; } = target;

    public IMigrationCheckpointStore CheckpointStore { get; } = checkpointStore;

    public IMigrationTargetReadiness Readiness { get; } = readiness;

    public TimeProvider TimeProvider { get; } = timeProvider;

    public ILoggerFactory LoggerFactory { get; } = loggerFactory;

    public GuidedMigrationArguments Arguments { get; } = arguments;

    public MigrationConsole Console { get; } = console;

    public MigrationEngineOptions Options { get; set; }

    public MigrationEngine Engine { get; set; }

    /// <summary>
    /// The required categories this run copies: the ones both the source and the target support.
    /// </summary>
    public IReadOnlyList<MigrationCategory> ToCopy { get; set; }

    public IReadOnlyList<MigrationCheckpoint> CheckpointsAtStart { get; set; }

    public MigrationRunState State { get; set; }

    /// <summary>
    /// The source's row count for each required category the rows step counted, so no later step reads it again.
    /// </summary>
    public Dictionary<string, long> Counts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The categories the operator chose to abandon before the copy. The copy step abandons them, so nothing is written before the operator agrees to start.
    /// </summary>
    public HashSet<string> PendingAbandons { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// True when the source would not open in a settled run, so nothing can be counted.
    /// </summary>
    public bool SourceUnavailable { get; set; }

    /// <summary>
    /// Where each copied category ended in the last copy pass.
    /// </summary>
    public IReadOnlyList<MigrationCheckpoint> Finished { get; set; }

    public int ExitCode { get; set; }
}
