namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// What one failed message document became: either a row with its failure group rows, or a skip with the reason.
/// </summary>
abstract record FailedMessageRowMapping
{
    FailedMessageRowMapping()
    {
    }

    /// <summary>
    /// The document maps to a failed message row and the group rows keyed by it.
    /// </summary>
    public sealed record Mapped(FailedMessageEntity Row, IReadOnlyList<FailedMessageGroupEntity> Groups) : FailedMessageRowMapping;

    /// <summary>
    /// The document cannot become a row, for the reason given, and <paramref name="Detail" /> says which document and why.
    /// </summary>
    public sealed record Skipped(MigrationSkipReason Reason, string Detail) : FailedMessageRowMapping;
}
