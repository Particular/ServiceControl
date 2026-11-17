#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationCategoryStateExtensionsTests
{
    [Test]
    public void Every_skip_reason_is_exactly_one_of_harmless_permanent_or_retryable()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Kinds.Keys, Is.EquivalentTo(Enum.GetValues<MigrationSkipReason>()), "a reason on neither list is treated as a fault a retry can fix, so each new reason has to be placed here on purpose");

            foreach (var (reason, kind) in Kinds)
            {
                Assert.That((reason.IsBenign(), reason.IsPermanent()), Is.EqualTo((kind == SkipKind.Harmless, kind == SkipKind.Permanent)), $"{reason} is {kind}");
            }
        }
    }

    enum SkipKind
    {
        Harmless,
        Permanent,
        Retryable
    }

    static readonly Dictionary<MigrationSkipReason, SkipKind> Kinds = new()
    {
        [MigrationSkipReason.BodyUnreadable] = SkipKind.Retryable,
        [MigrationSkipReason.PastRetention] = SkipKind.Harmless,
        [MigrationSkipReason.RequiredValueMissing] = SkipKind.Permanent,
        [MigrationSkipReason.BlankGroupComment] = SkipKind.Harmless,
        [MigrationSkipReason.SourceStorageCheck] = SkipKind.Harmless,
        [MigrationSkipReason.UnparseableUniqueMessageId] = SkipKind.Permanent,
        [MigrationSkipReason.NoProcessingAttempts] = SkipKind.Permanent,
        [MigrationSkipReason.ValueTooLong] = SkipKind.Permanent,
        [MigrationSkipReason.MalformedValue] = SkipKind.Permanent,
        [MigrationSkipReason.BodyMissing] = SkipKind.Permanent,
        [MigrationSkipReason.Unknown] = SkipKind.Retryable
    };
}
