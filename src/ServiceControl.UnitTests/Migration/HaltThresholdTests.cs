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
        // Exactly the floor, which settles it before the proportion is ever worked out. "Exceed" means strictly past, not "at".
        var exceeded = HaltThreshold.Exceeded(skippedCount: 100, totalCount: 2_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void Exactly_on_the_proportion_does_not_halt_once_the_floor_is_behind_it()
    {
        // 101 of 2,020 is exactly 5% with the floor already passed, so this is the only shape that
        // reaches the proportion comparison and depends on it being strictly greater.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 101, totalCount: 2_020, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void A_hair_past_the_proportion_halts_once_the_floor_is_behind_it()
    {
        // 101 of 2,000 is 5.05%: the same skip count as above, one row's worth over the line.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 101, totalCount: 2_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.True);
    }

    [Test]
    public void A_large_category_losing_a_small_share_does_not_halt_once_past_the_floor()
    {
        // 101 of 5,000,000 is 0.002%. The floor is already behind it, so only the proportion can stop a halt
        // here, and a rule that halted on the floor alone would stop a healthy copy of a huge category.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 101, totalCount: 5_000_000, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void Losing_every_row_of_a_run_is_losing_most_of_it()
    {
        Assert.That(HaltThreshold.MostOfItWasLost(skippedCount: 90, totalCount: 90), Is.True);
    }

    [Test]
    public void Losing_all_but_one_row_of_a_run_is_losing_most_of_it()
    {
        Assert.That(HaltThreshold.MostOfItWasLost(skippedCount: 99, totalCount: 100), Is.True);
    }

    [Test]
    public void Losing_exactly_half_a_run_is_not_losing_most_of_it()
    {
        // The rule needs strictly more than half, so an exact half is the one input that separates it from a
        // rule that halted at half as well.
        Assert.That(HaltThreshold.MostOfItWasLost(skippedCount: 50, totalCount: 100), Is.False);
    }

    [Test]
    public void A_run_that_processed_nothing_lost_nothing()
    {
        Assert.That(HaltThreshold.MostOfItWasLost(skippedCount: 0, totalCount: 0), Is.False);
    }

    [Test]
    public void A_skip_count_with_nothing_processed_never_halts_and_never_divides_by_zero()
    {
        // Past the floor with a zero total, which is the only input that reaches the division guard.
        var exceeded = HaltThreshold.Exceeded(skippedCount: 101, totalCount: 0, percentThreshold: 5, minimumFloor: 100);

        Assert.That(exceeded, Is.False);
    }

    [Test]
    public void No_rows_processed_yet_never_halts()
    {
        Assert.That(HaltThreshold.Exceeded(skippedCount: 0, totalCount: 0, percentThreshold: 5, minimumFloor: 100), Is.False);
    }
}
