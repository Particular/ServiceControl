#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationCategoryStateExtensionsTests
{
    [Test]
    public void Exactly_the_two_harmless_reasons_are_harmless_and_every_other_skip_reason_is_a_fault() =>
        Assert.That(
            Enum.GetValues<MigrationSkipReason>().Where(reason => reason.IsBenign()),
            Is.EquivalentTo(new[] { MigrationSkipReason.PastRetention, MigrationSkipReason.BlankGroupComment }),
            "a harmless reason is exempt from the halt threshold and the settle rule, so any number of rows lost to one settles the category Done");
}
