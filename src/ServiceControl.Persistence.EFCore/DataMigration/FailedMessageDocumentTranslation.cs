namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// What one failed message document became: either a row with its failure group rows, or a skip with the reason.
/// </summary>
abstract record FailedMessageDocumentTranslation
{
    FailedMessageDocumentTranslation()
    {
    }

    /// <summary>
    /// The document translates to a failed message row and the group rows keyed by it.
    /// Changes has one sentence for each way the row differs from the document on purpose, and is empty when it differs in none.
    /// </summary>
    public sealed record Translated(FailedMessageEntity Row, IReadOnlyList<FailedMessageGroupEntity> Groups, IReadOnlyList<string> Changes) : FailedMessageDocumentTranslation;

    /// <summary>
    /// The document cannot become a row, for the reason given, and <paramref name="Detail" /> says which document and why.
    /// </summary>
    public sealed record Skipped(MigrationSkipReason Reason, string Detail) : FailedMessageDocumentTranslation;
}
