namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

static class QueueAddressQueries
{
    /// <summary>
    /// Both fields of every address the body shows, plus the total behind Total-Count.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<QueueAddress> page, long totalCount) =>
        new(DataVersion.OverRows([("addresses", totalCount)], page,
                address => [address.PhysicalAddress, address.FailedMessageCount]),
            totalCount,
            false);
}
