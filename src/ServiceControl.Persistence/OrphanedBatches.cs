namespace ServiceControl.Persistence
{
    using System.Collections.Generic;

    public record OrphanedBatches(IReadOnlyList<RetryBatch> Batches, bool MightBeIncomplete);
}
