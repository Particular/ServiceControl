#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
class FailedMessageRowMapperTests
{
    static readonly DateTime StatusChangedAtFromTheSource = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    static readonly DateTime LastModifiedFromTheSource = new(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Carries_the_unique_message_id_across_unchanged()
    {
        var uniqueMessageId = Guid.NewGuid();
        var failure = new IngestedFailure();
        var document = failure.ToFailedMessage();
        document.UniqueMessageId = uniqueMessageId.ToString();

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(row.UniqueMessageId, Is.EqualTo(uniqueMessageId));
    }

    [Test]
    public void Skips_a_unique_message_id_that_is_not_a_guid()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.UniqueMessageId = "not-a-guid";

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.UnparseableUniqueMessageId));
            Assert.That(skip.Detail, Does.Contain("not-a-guid"));
        }
    }

    [Test]
    public void Skips_a_document_with_no_processing_attempts()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts.Clear();

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.NoProcessingAttempts));
            Assert.That(skip.Detail, Does.Contain(document.UniqueMessageId));
        }
    }

    [Test]
    public void Skips_a_document_with_no_processing_attempts_that_the_source_sent_with_no_StatusChangedAt()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts.Clear();
        var complete = Row(document);
        var row = complete with { Metadata = new Dictionary<string, object?>(complete.Metadata) { [MigrationMetadataKeys.StatusChangedAt] = null } };

        var skip = SkipOf(FailedMessageRowMapper.Map(row));

        Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.NoProcessingAttempts), "the source resolves StatusChangedAt from the newest attempt, so an attemptless document always arrives without one");
    }

    [Test]
    public void Skips_a_document_whose_newest_attempt_has_no_failing_endpoint_address()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts[^1].FailureDetails.AddressOfFailingEndpoint = null!;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing), "FailingEndpointAddress is NOT NULL, so the insert would fail and halt the category");
            Assert.That(skip.Detail, Does.Contain(nameof(FailedMessageEntity.FailingEndpointAddress)).And.Contain(document.UniqueMessageId), "the log has to name the column and the message an operator would look up");
        }
    }

    [TestCase(nameof(FailedMessage.ProcessingAttempt.FailureDetails))]
    [TestCase(nameof(FailedMessage.ProcessingAttempt.MessageMetadata))]
    [TestCase(nameof(FailedMessage.ProcessingAttempt.Headers))]
    public void Skips_a_newest_attempt_with_no_value_the_row_is_built_from(string missing)
    {
        var document = new IngestedFailure().ToFailedMessage();
        var newest = document.ProcessingAttempts[^1];
        switch (missing)
        {
            case nameof(newest.FailureDetails):
                newest.FailureDetails = null!;
                break;
            case nameof(newest.MessageMetadata):
                newest.MessageMetadata = null!;
                break;
            default:
                newest.Headers = null!;
                break;
        }

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing));
            Assert.That(skip.Detail, Does.Contain(missing).And.Contain(document.UniqueMessageId), "the log has to name what is missing and the message an operator would look up");
        }
    }

    [TestCase("TimeSent", "garbage", "garbage")]
    [TestCase("MessageType", 43L, "43")]
    [TestCase("ConversationId", 44L, "44")]
    [TestCase("IsSystemMessage", "yes", "yes")]
    public void Skips_a_value_present_with_the_wrong_type_and_names_the_key_the_type_found_and_the_value(string key, object malformed, string shown)
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts[^1].MessageMetadata[key] = malformed;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.MalformedValue), "copying it as null or false would lose a value the source holds without a skip to show for it");
            Assert.That(skip.Detail, Does.Contain(key).And.Contain(malformed.GetType().FullName).And.Contain(document.UniqueMessageId));
            Assert.That(skip.Detail, Does.Contain($"'{shown}'"), "the operator should not have to open RavenDB to see the value");
        }
    }

    [Test]
    public void Shows_only_the_first_100_characters_of_a_malformed_value()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts[^1].MessageMetadata["TimeSent"] = new string('a', 100) + new string('b', 400);

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(skip.Detail, Does.Contain($"'{new string('a', 100)}...'").And.Not.Contain(new string('a', 100) + "b"), "a stored value can be any length, and the detail is one log line");
    }

    [Test]
    public void Skips_a_newest_attempt_whose_FailureDetails_key_was_missing_from_the_document()
    {
        var document = new IngestedFailure().ToFailedMessage();
        // What RavenDB hands back for an attempt stored without the key.
        document.ProcessingAttempts[^1].FailureDetails = new FailedMessage.ProcessingAttempt().FailureDetails;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing), "copying it would store an empty failing address to retry to and a time of failure that is the time it was read");
            Assert.That(skip.Detail, Does.Contain(nameof(FailedMessageEntity.FailingEndpointAddress)).And.Contain(document.UniqueMessageId));
        }
    }

    [TestCase("TimeOfFailure")]
    [TestCase("AttemptedAt")]
    public void Skips_a_newest_attempt_with_no_time_it_failed_or_was_attempted(string missing)
    {
        var document = new IngestedFailure().ToFailedMessage();
        var newest = document.ProcessingAttempts[^1];
        if (missing == "TimeOfFailure")
        {
            newest.FailureDetails.TimeOfFailure = default;
        }
        else
        {
            newest.AttemptedAt = default;
        }

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing), "the year 1 is not a time the message failed");
            Assert.That(skip.Detail, Does.Contain(missing).And.Contain(document.UniqueMessageId));
        }
    }

    [Test]
    public void Leaves_an_older_attempt_whose_FailureDetails_key_was_missing_out_of_the_first_and_last_failure_times()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[0].FailureDetails = new FailedMessage.ProcessingAttempt().FailureDetails;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[1].FailureDetails.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[2].FailureDetails.TimeOfFailure), "the default object's time of failure is the time it was read, which would become the last failure");
        }
    }

    [Test]
    public void Leaves_an_older_attempt_with_no_time_of_failure_out_of_the_first_failure_time()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[0].FailureDetails.TimeOfFailure = default;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(row.FirstTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[1].FailureDetails.TimeOfFailure), "the year 1 is not a time the message failed");
    }

    [Test]
    public void Skips_a_document_whose_processing_attempts_list_is_null()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts = null!;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.NoProcessingAttempts));
    }

    [Test]
    public void Maps_a_null_failure_groups_list_to_no_groups()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.FailureGroups = null!;

        var mapping = FailedMessageRowMapper.Map(Row(document));

        Assert.That(((FailedMessageRowMapping.Mapped)mapping).Groups, Is.Empty);
    }

    [Test]
    public void Drops_a_failure_group_entry_that_is_null()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.FailureGroups = [null!, new() { Id = "group-1", Title = "Title 1", Type = "Type 1" }];

        var mapping = FailedMessageRowMapper.Map(Row(document));

        Assert.That(((FailedMessageRowMapping.Mapped)mapping).Groups.Select(group => group.GroupId), Is.EqualTo(new[] { "group-1" }));
    }

    [TestCase("SendingEndpoint", "not an endpoint")]
    [TestCase("ReceivingEndpoint", 42)]
    public void Refuses_an_endpoint_that_is_not_an_EndpointDetails_rather_than_copying_it_as_null(string key, object wrongType)
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts[^1].MessageMetadata[key] = wrongType;

        Assert.Throws<InvalidCastException>(() => FailedMessageRowMapper.Map(Row(document)), "the source restores every endpoint before the mapper sees it, so a wrong type is a defect that must halt the copy, not a row with empty endpoint columns");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Maps_a_TimeSent_or_endpoint_that_is_missing_or_null_to_null(bool keyPresent)
    {
        var document = new IngestedFailure().ToFailedMessage();
        var metadata = document.ProcessingAttempts[^1].MessageMetadata;
        foreach (var key in new[] { "TimeSent", "SendingEndpoint", "ReceivingEndpoint" })
        {
            if (keyPresent)
            {
                metadata[key] = null!;
            }
            else
            {
                metadata.Remove(key);
            }
        }

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.TimeSent, Is.Null);
            Assert.That(new[] { row.SendingEndpointName, row.SendingEndpointHost, row.ReceivingEndpointName, row.ReceivingEndpointHost }, Is.All.Null, "RavenDB's own read path shows a missing endpoint as null");
            Assert.That(new[] { row.SendingEndpointHostId, row.ReceivingEndpointHostId }, Is.All.Null);
        }
    }

    [Test]
    public void Skips_a_document_whose_newest_attempt_is_null()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[2] = null!;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing));
            Assert.That(skip.Detail, Does.Contain(document.UniqueMessageId));
        }
    }

    [Test]
    public void Takes_the_earliest_failure_from_the_attempts_that_have_failure_details()
    {
        var document = ThreeAttempts();
        var earliestWithDetails = document.ProcessingAttempts[1].FailureDetails.TimeOfFailure;
        document.ProcessingAttempts[0].FailureDetails = null!;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(earliestWithDetails), "an older attempt with no failure details has no time to offer, and must not halt the category");
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[2].FailureDetails.TimeOfFailure));
        }
    }

    [Test]
    public void Ignores_an_earlier_attempt_that_is_null()
    {
        var document = ThreeAttempts();
        var earliestRemaining = document.ProcessingAttempts[1].FailureDetails.TimeOfFailure;
        document.ProcessingAttempts[0] = null!;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(3), "RavenDB's read path counts every stored attempt, null ones included");
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(earliestRemaining));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[2].FailureDetails.TimeOfFailure));
        }
    }

    [Test]
    public void Maps_when_only_an_earlier_attempt_has_a_value_of_the_wrong_type()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[0].MessageMetadata["TimeSent"] = "garbage";
        document.ProcessingAttempts[0].MessageMetadata["ReceivingEndpoint"] = "not an endpoint";

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(row.FailingEndpointAddress, Is.EqualTo("newest-address"), "only the newest attempt's metadata becomes the row");
    }

    [Test]
    public void Keeps_the_last_450_characters_of_a_message_type_longer_than_the_column()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var longType = new string('a', 100) + new string('b', 450);
        document.ProcessingAttempts[^1].MessageMetadata["MessageType"] = longType;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(row.MessageType, Is.EqualTo(new string('b', 450)));
    }

    [Test]
    public void Takes_the_newest_attempt_for_the_row_and_the_earliest_and_latest_failure_across_all()
    {
        var document = ThreeAttempts();

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(3));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[0].FailureDetails.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(document.ProcessingAttempts[2].FailureDetails.TimeOfFailure));
            Assert.That(row.LastAttemptedAt, Is.EqualTo(document.ProcessingAttempts[2].AttemptedAt));
            Assert.That(row.FailingEndpointAddress, Is.EqualTo("newest-address"));
            Assert.That(row.ExceptionMessage, Is.EqualTo("newest-exception"));
        }
    }

    [Test]
    public void Skips_when_only_the_newest_of_three_attempts_has_no_failing_endpoint_address()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[2].FailureDetails.AddressOfFailingEndpoint = null!;

        var skip = SkipOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(skip.Reason, Is.EqualTo(MigrationSkipReason.RequiredValueMissing));
    }

    [Test]
    public void Maps_when_only_an_earlier_attempt_has_no_failing_endpoint_address()
    {
        var document = ThreeAttempts();
        document.ProcessingAttempts[0].FailureDetails.AddressOfFailingEndpoint = null!;

        var row = RowOf(FailedMessageRowMapper.Map(Row(document)));

        Assert.That(row.FailingEndpointAddress, Is.EqualTo("newest-address"));
    }

    [Test]
    public void Maps_each_group_with_an_id_to_a_row_keyed_by_the_message_and_drops_a_group_with_no_id()
    {
        var uniqueMessageId = Guid.NewGuid();
        var document = new IngestedFailure().ToFailedMessage();
        document.UniqueMessageId = uniqueMessageId.ToString();
        document.FailureGroups =
        [
            new() { Id = "group-1", Title = "Title 1", Type = "Type 1" },
            new() { Id = "group-2", Title = null, Type = null },
            new() { Id = null!, Title = "No id", Type = "Type" }
        ];

        var mapping = (FailedMessageRowMapping.Mapped)FailedMessageRowMapper.Map(Row(document));

        Assert.That(
            mapping.Groups.Select(group => (group.FailedMessageUniqueId, group.GroupId, group.Title, group.Type)),
            Is.EqualTo(new[]
            {
                (uniqueMessageId, "group-1", "Title 1", "Type 1"),
                (uniqueMessageId, "group-2", string.Empty, string.Empty)
            }));
    }

    [TestCase(FailedMessageStatus.Unresolved)]
    [TestCase(FailedMessageStatus.Resolved)]
    [TestCase(FailedMessageStatus.RetryIssued)]
    [TestCase(FailedMessageStatus.Archived)]
    public void Preserves_the_stored_status(FailedMessageStatus status)
    {
        var row = RowOf(FailedMessageRowMapper.Map(Row(new IngestedFailure().ToFailedMessage(status))));

        Assert.That(row.Status, Is.EqualTo(status));
    }

    [Test]
    public void Stores_the_StatusChangedAt_the_source_resolved()
    {
        var row = RowOf(FailedMessageRowMapper.Map(Row(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived))));

        Assert.That(row.StatusChangedAt, Is.EqualTo(StatusChangedAtFromTheSource));
    }

    [Test]
    public void Stores_the_LastModified_the_source_carried()
    {
        var row = RowOf(FailedMessageRowMapper.Map(Row(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived))));

        Assert.That(row.LastModified, Is.EqualTo(LastModifiedFromTheSource), "RavenDB's @last-modified, not the copy time");
    }

    [TestCase(MigrationMetadataKeys.StatusChangedAt)]
    [TestCase(MigrationMetadataKeys.LastModified)]
    public void Refuses_a_row_the_source_sent_without_a_resolved_value(string missingKey)
    {
        var complete = Row(new IngestedFailure().ToFailedMessage());
        var row = complete with { Metadata = complete.Metadata.Where(entry => entry.Key != missingKey).ToDictionary(entry => entry.Key, entry => entry.Value) };

        var ex = Assert.Throws<InvalidOperationException>(() => FailedMessageRowMapper.Map(row));

        Assert.That(ex.Message, Does.Contain(missingKey).And.Contain(row.SourceId));
    }

    [TestCase(MigrationMetadataKeys.StatusChangedAt)]
    [TestCase(MigrationMetadataKeys.LastModified)]
    public void Refuses_a_resolved_value_of_the_wrong_type_and_names_the_type_it_found(string key)
    {
        var complete = Row(new IngestedFailure().ToFailedMessage());
        var row = complete with { Metadata = new Dictionary<string, object?>(complete.Metadata) { [key] = "2026-09-01T08:00:00Z" } };

        var ex = Assert.Throws<InvalidOperationException>(() => FailedMessageRowMapper.Map(row));

        Assert.That(ex.Message, Does.Contain(typeof(string).FullName).And.Contain(key).And.Contain(row.SourceId), "a value that is present but mistyped must not read as missing");
    }

    [TestCase(DateTimeKind.Unspecified)]
    [TestCase(DateTimeKind.Local)]
    public void Writes_every_date_as_UTC_whatever_kind_it_arrived_with(DateTimeKind kind)
    {
        var document = ThreeAttempts();
        foreach (var attempt in document.ProcessingAttempts)
        {
            attempt.FailureDetails.TimeOfFailure = DateTime.SpecifyKind(attempt.FailureDetails.TimeOfFailure, kind);
            attempt.AttemptedAt = DateTime.SpecifyKind(attempt.AttemptedAt, kind);
        }

        var newest = document.ProcessingAttempts[^1];
        var timeSent = new DateTime(2026, 7, 22, 9, 59, 0, kind);
        newest.MessageMetadata["TimeSent"] = timeSent;
        var statusChangedAt = DateTime.SpecifyKind(StatusChangedAtFromTheSource, kind);
        var lastModified = DateTime.SpecifyKind(LastModifiedFromTheSource, kind);
        var complete = Row(document);
        var source = complete with
        {
            Metadata = new Dictionary<string, object?>
            {
                [MigrationMetadataKeys.StatusChangedAt] = statusChangedAt,
                [MigrationMetadataKeys.LastModified] = lastModified
            }
        };

        var row = RowOf(FailedMessageRowMapper.Map(source));

        DateTime[] written = [row.StatusChangedAt, row.LastModified, row.FirstTimeOfFailure, row.LastTimeOfFailure, row.LastAttemptedAt, row.TimeSent!.Value];
        DateTime[] expected = [statusChangedAt, lastModified, document.ProcessingAttempts[0].FailureDetails.TimeOfFailure, newest.FailureDetails.TimeOfFailure, newest.AttemptedAt, timeSent];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written.Select(value => value.Kind), Is.All.EqualTo(DateTimeKind.Utc), "Npgsql refuses to write a DateTime of any other kind to a timestamp with time zone column");
            Assert.That(written, Is.EqualTo(expected.Select(value => kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc))), "a value with no kind is already UTC and must not be shifted by the local offset");
        }
    }

    static FailedMessageEntity RowOf(FailedMessageRowMapping mapping)
    {
        Assert.That(mapping, Is.TypeOf<FailedMessageRowMapping.Mapped>());
        return ((FailedMessageRowMapping.Mapped)mapping).Row;
    }

    static FailedMessageRowMapping.Skipped SkipOf(FailedMessageRowMapping mapping)
    {
        Assert.That(mapping, Is.TypeOf<FailedMessageRowMapping.Skipped>());
        return (FailedMessageRowMapping.Skipped)mapping;
    }

    static FailedMessage ThreeAttempts()
    {
        var failure = new IngestedFailure();
        var document = failure.ToFailedMessage(numberOfAttempts: 3);
        for (var i = 0; i < 3; i++)
        {
            var attempt = document.ProcessingAttempts[i];
            attempt.FailureDetails.TimeOfFailure = new DateTime(2026, 7, 22, 10 + i, 0, 0, DateTimeKind.Utc);
            attempt.AttemptedAt = attempt.FailureDetails.TimeOfFailure;
        }

        var newest = document.ProcessingAttempts[2];
        newest.FailureDetails.AddressOfFailingEndpoint = "newest-address";
        newest.FailureDetails.Exception!.Message = "newest-exception";
        return document;
    }

    // Every row carries the two dates the source resolves, as a real source's rows do.
    static MigrationRow Row(FailedMessage document) =>
        new(
            $"FailedMessages/{document.UniqueMessageId}",
            document,
            new Dictionary<string, object?>
            {
                [MigrationMetadataKeys.StatusChangedAt] = StatusChangedAtFromTheSource,
                [MigrationMetadataKeys.LastModified] = LastModifiedFromTheSource
            });
}
