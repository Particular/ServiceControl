namespace ServiceControl.Infrastructure.Ingestion;

using System;

/// <summary>
/// How an <see cref="IngestionPipeline" /> trades latency for throughput.
/// </summary>
public sealed record IngestionPipelineSettings
{
    /// <summary>
    /// The most messages one write handles.
    /// </summary>
    public required int BatchSize { get; init; }

    /// <summary>
    /// How many batches are written at once. Only raise this for a storage that is safe under
    /// concurrent writers: batches commit in whatever order they finish.
    /// </summary>
    public int MaxWriters { get; init; } = 1;

    /// <summary>
    /// How long a batch that is not yet full waits for more messages. Zero writes whatever has
    /// arrived, which costs throughput at a trickle and costs nothing at volume.
    /// </summary>
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.Zero;
}