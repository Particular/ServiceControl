namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;

class DateTimeKindTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Timestamps_are_read_back_as_utc()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.TimeSent.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.LastAttemptedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.FirstTimeOfFailure.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.LastTimeOfFailure.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.LastModified.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.StatusChangedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }
}
