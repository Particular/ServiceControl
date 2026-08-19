namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class IngestionClockTests : IngestionTestBase
{
    [Test]
    public async Task A_re_ingested_message_moves_the_version()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var before = (await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null)).Version;

        AdvanceClock(TimeSpan.FromMinutes(5));

        await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(5)));
        await CompleteDatabaseOperation();

        var after = (await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null)).Version;

        // The EF clock is frozen, so without AdvanceClock a second attempt at the same message leaves
        // both the count and LastModified alone and the version cannot move.
        VersionAssert.Moved(before, after,
            "the stored body changed, so a revalidating client must not be served the old bytes");
    }
}
