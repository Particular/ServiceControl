namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class BodyVersionTests : IngestionTestBase
{
    static readonly DateTime FirstAttempt = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Version_changes_when_a_later_attempt_carries_a_different_body()
    {
        var first = Failure("the original body", FirstAttempt);

        await Ingest(first);
        await CompleteDatabaseOperation();

        var (originalBody, before) = await Fetch(first.UniqueMessageIdString);

        // The same message fails again with a different body. Ingestion updates the existing row
        // rather than adding one, so the message id is unchanged and cannot serve as a version.
        AdvanceClock(TimeSpan.FromHours(8));
        await Ingest(Failure("a completely different body", FirstAttempt.AddHours(8), first));
        await CompleteDatabaseOperation();

        var (replacedBody, after) = await Fetch(first.UniqueMessageIdString);

        Assert.Multiple(() =>
        {
            Assert.That(originalBody, Is.EqualTo("the original body"));
            Assert.That(replacedBody, Is.EqualTo("a completely different body"), "the stored body was replaced");
            Assert.That(before.HasValue, Is.True, "there was no version to move");
            Assert.That(after.Matches(before), Is.False,
                "the body changed, so a client holding the old version must be sent the new body rather than a 304");
        });
    }

    [Test]
    public async Task Version_is_stable_while_the_body_is_not_rewritten()
    {
        var failure = Failure("the original body", FirstAttempt);

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var (_, first) = await Fetch(failure.UniqueMessageIdString);
        var (_, second) = await Fetch(failure.UniqueMessageIdString);

        Assert.That(second.Matches(first), Is.True,
            "two reads with no write between them must let a client revalidate successfully");
    }

    [Test]
    public async Task A_message_with_no_body_reads_back_as_empty_on_every_backend()
    {
        var failure = Failure(string.Empty, FirstAttempt);

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var result = await BodyStorage.TryFetch(failure.UniqueMessageIdString);

        // Empty carries no content and so no version, whatever this task does. What is worth pinning
        // is that all persistence seams agree it is Empty rather than NotFound or Unavailable, which is
        // what decides whether the caller gets a no-body response or a 404.
        Assert.That(result.State, Is.EqualTo(MessageBodyState.Empty));
    }

    async Task<(string Body, DataVersion Version)> Fetch(string bodyId)
    {
        var result = await BodyStorage.TryFetch(bodyId);

        Assert.That(result.State, Is.EqualTo(MessageBodyState.Available));

        await using var stream = result.Content.Stream;
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return (await reader.ReadToEndAsync(), result.Content.Version);
    }

    static IngestedFailure Failure(string body, DateTime attemptedAt, IngestedFailure sameMessageAs = null)
    {
        var identity = sameMessageAs ?? new IngestedFailure();

        return new IngestedFailure
        {
            MessageId = identity.MessageId,
            EndpointName = identity.EndpointName,
            Body = Encoding.UTF8.GetBytes(body),
            AttemptedAt = attemptedAt,
            TimeOfFailure = attemptedAt
        };
    }
}
