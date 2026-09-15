namespace ServiceControl.UnitTests.Migration;

using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class HaltThresholdTests
{
    [Test]
    public void Does_not_halt_a_three_row_category_with_one_bad_row()
    {
        // 1 of 3 is 33%, well past the 5% proportion, but 1 skip never passes the 100-row floor.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 1, totalCount: 3, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void Halts_a_large_category_with_a_systemic_failure()
    {
        // 600 of 10,000 is 6%, past both the proportion and the floor.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 600, totalCount: 10_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.True);
    }

    [Test]
    public void Does_not_halt_a_large_category_with_only_a_small_proportion_skipped()
    {
        // 10,000 of 5,000,000 is 0.2%: past the floor, nowhere near the proportion.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 10_000, totalCount: 5_000_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void Exactly_at_both_boundaries_does_not_halt_because_both_must_be_exceeded()
    {
        // 100 of 2,000 is exactly 5% and exactly the floor. "Exceed" means strictly past, not "at".
        var exceeded = HaltThreshold.Exceeded(skippedCount: 100, totalCount: 2_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void No_rows_processed_yet_never_halts()
    {
        Assert.That(HaltThreshold.Exceeded(skippedCount: 0, totalCount: 0, percentThreshold: 5, minimumFloor: 100), Is.False);
    }
}
