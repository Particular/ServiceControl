namespace ServiceControl.Persistence
{
    using System.Collections.Generic;

    // An eventually-consistent store can miss batches its index has not caught up with yet, so the sweep
    // keeps rechecking while that is possible.
    public record OrphanedBatches(IReadOnlyList<RetryBatch> Batches, bool MightBeIncomplete)
    {
        public static OrphanedBatches Complete(IReadOnlyList<RetryBatch> batches) => new(batches, false);
    }
}
