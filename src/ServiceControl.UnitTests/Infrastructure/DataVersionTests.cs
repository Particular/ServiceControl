namespace ServiceControl.UnitTests.Infrastructure;

using System;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
public class DataVersionTests
{
    [Test]
    public void None_never_matches_itself()
    {
        Assert.That(DataVersion.None.Matches(DataVersion.None), Is.False,
            "two callers who both know nothing have not established that nothing changed, and matching here would answer 304 for every request");
    }

    [Test]
    public void None_never_matches_a_real_version()
    {
        var real = DataVersion.FromToken("4611686018427387904");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataVersion.None.Matches(real), Is.False);
            Assert.That(real.Matches(DataVersion.None), Is.False);
        }
    }

    [Test]
    public void Equality_stays_reflexive_so_the_struct_is_safe_in_collections()
    {
        // Matches carries the cache rule, Equals must not, or any dictionary or Distinct over it breaks.
        Assert.That(DataVersion.None.Equals(DataVersion.None), Is.True);
    }

    [Test]
    public void An_identical_token_matches()
    {
        Assert.That(DataVersion.FromToken("abc").Matches(DataVersion.FromToken("abc")), Is.True);
    }

    [Test]
    public void A_different_token_does_not_match()
    {
        Assert.That(DataVersion.FromToken("abc").Matches(DataVersion.FromToken("abd")), Is.False);
    }

    [Test]
    public void An_empty_token_is_absent()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataVersion.FromToken(null).HasValue, Is.False);
            Assert.That(DataVersion.FromToken(string.Empty).HasValue, Is.False);
        }
    }

    [Test]
    public void A_numeric_token_is_rendered_invariantly()
    {
        Assert.That(DataVersion.FromToken(4611686018427387904L).ToString(), Is.EqualTo("4611686018427387904"));
    }

    [Test]
    public void Compose_is_stable_across_calls()
    {
        var first = DataVersion.Compose(("total", 3L), ("highestId", 7L));
        var second = DataVersion.Compose(("total", 3L), ("highestId", 7L));

        Assert.That(first.Matches(second), Is.True);
    }

    [Test]
    public void Compose_moves_when_any_term_value_moves()
    {
        var before = DataVersion.Compose(("total", 3L), ("highestId", 7L));
        var after = DataVersion.Compose(("total", 3L), ("highestId", 8L));

        Assert.That(before.Matches(after), Is.False);
    }

    [Test]
    public void Compose_distinguishes_terms_that_bare_concatenation_would_collide()
    {
        var first = DataVersion.Compose(("a", 1L), ("b", 23L));
        var second = DataVersion.Compose(("a", 12L), ("b", 3L));

        Assert.That(first.Matches(second), Is.False);
    }

    [Test]
    public void Compose_moves_when_a_term_goes_from_absent_to_present()
    {
        DateTime? absent = null;
        DateTime? present = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        var empty = DataVersion.Compose(("newest", absent));
        var populated = DataVersion.Compose(("newest", present));

        Assert.That(empty.Matches(populated), Is.False);
    }

    [Test]
    public void Compose_with_no_terms_is_absent()
    {
        Assert.That(DataVersion.Compose().HasValue, Is.False);
    }

    [Test]
    public void OverRows_moves_when_a_row_changes_under_an_unchanged_timestamp()
    {
        var at = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        var before = Page(("a", at, "Unresolved"));
        var after = Page(("a", at, "Archived"));

        Assert.That(after.Matches(before), Is.False,
            "two writes to one row inside a single clock tick leave the timestamp identical, so the version cannot rest on it alone");
    }

    [Test]
    public void OverRows_distinguishes_two_pages_of_one_set()
    {
        var at = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        var firstPage = Page(("a", at, "Unresolved"));
        var secondPage = Page(("b", at, "Unresolved"));

        Assert.That(secondPage.Matches(firstPage), Is.False,
            "the two pages render different rows, so a client holding one must not be told the other is current");
    }

    [Test]
    public void OverRows_holds_while_the_page_and_the_total_hold()
    {
        var at = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.That(Page(("a", at, "Unresolved")).Matches(Page(("a", at, "Unresolved"))), Is.True);
    }

    [Test]
    public void OverRows_moves_when_only_the_total_moves()
    {
        var at = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var row = ("a", at, "Unresolved");

        Assert.That(DataVersion.OverRows([("total", 9L)], [row], Fields)
                .Matches(DataVersion.OverRows([("total", 2L)], [row], Fields)), Is.False,
            "Total-Count is part of the response, so a client holding the old one must not be told it is current");
    }

    [Test]
    public void OverRows_distinguishes_rows_that_bare_concatenation_would_collide()
    {
        var left = DataVersion.OverRows([("total", 1L)], [("ab", "c")], row => [row.Item1, row.Item2]);
        var right = DataVersion.OverRows([("total", 1L)], [("a", "bc")], row => [row.Item1, row.Item2]);

        Assert.That(right.Matches(left), Is.False,
            "fields inside a row are length prefixed, so no value can pose as a different split of the same text");
    }

    [Test]
    public void OverRows_refuses_a_missing_row_source()
    {
        Assert.Throws<ArgumentNullException>(() => DataVersion.OverRows<string>([("total", 0L)], null, _ => []));
    }

    [Test]
    public void OverRows_refuses_a_missing_field_selector()
    {
        Assert.Throws<ArgumentNullException>(() => DataVersion.OverRows<string>([("total", 0L)], [], null));
    }

    static DataVersion Page(params (string Id, DateTime At, string Status)[] rows) =>
        DataVersion.OverRows([("total", 2L)], rows, Fields);

    static object[] Fields((string Id, DateTime At, string Status) row) => [row.Id, row.At, row.Status];

    [Test]
    public void Combine_does_not_depend_on_the_order_instances_answered_in()
    {
        var a = DataVersion.FromToken("a");
        var b = DataVersion.FromToken("b");

        Assert.That(DataVersion.Combine([("one", a), ("two", b)])
            .Matches(DataVersion.Combine([("two", b), ("one", a)])), Is.True);
    }

    [Test]
    public void Combine_moves_when_two_instances_swap_which_version_they_report()
    {
        var a = DataVersion.FromToken("a");
        var b = DataVersion.FromToken("b");

        Assert.That(DataVersion.Combine([("one", a), ("two", b)])
                .Matches(DataVersion.Combine([("one", b), ("two", a)])), Is.False,
            "both instances changed, so a composite that only looked at the set of validators would answer 304 over stale data");
    }

    [Test]
    public void Combine_differs_from_every_instance_version_it_covers()
    {
        var a = DataVersion.FromToken("a");
        var b = DataVersion.FromToken("b");

        var combined = DataVersion.Combine([("one", a), ("two", b)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(combined.Matches(a), Is.False);
            Assert.That(combined.Matches(b), Is.False);
        }
    }

    [Test]
    public void Combine_is_absent_when_any_instance_has_no_version()
    {
        var combined = DataVersion.Combine([("one", DataVersion.FromToken("a")), ("two", DataVersion.None)]);

        Assert.That(combined.HasValue, Is.False,
            "a composite that ignored an instance would stop reporting that instance's changes");
    }

    [Test]
    public void Combine_of_nothing_is_absent()
    {
        Assert.That(DataVersion.Combine([]).HasValue, Is.False);
    }

    [Test]
    public void Compose_distinguishes_a_term_value_that_carries_the_delimiters()
    {
        var forged = DataVersion.Compose(("one", "a|two:1:b"));
        var genuine = DataVersion.Compose(("one", "a"), ("two", "b"));

        Assert.That(forged.Matches(genuine), Is.False,
            "a value able to pose as a longer term list would let a peer pin the composite version");
    }

    [Test]
    public void Compose_refuses_a_term_whose_text_is_not_derived_from_its_content()
    {
        Assert.That(() => DataVersion.Compose(("rows", new object())), Throws.ArgumentException,
            "a type name is a constant, so the version would never move and clients would cache forever");
    }

    [TestCase("\"abc\"", TestName = "FromClient_reads_a_quoted_validator")]
    [TestCase("W/\"abc\"", TestName = "FromClient_reads_a_weak_validator")]
    [TestCase("abc", TestName = "FromClient_reads_an_unquoted_validator_from_an_older_instance")]
    public void FromClient_yields_the_bare_validator(string headerValue)
    {
        Assert.That(DataVersion.FromClient(headerValue).ToString(), Is.EqualTo("abc"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("\"\"")]
    public void FromClient_treats_a_blank_validator_as_absent(string headerValue)
    {
        Assert.That(DataVersion.FromClient(headerValue).HasValue, Is.False,
            "a caller holding nothing must be a cache miss, never a match");
    }

    [Test]
    public void FromClient_leaves_a_malformed_validator_alone_rather_than_truncating_it()
    {
        // Stripping every quote would truncate this into something that might match by accident.
        Assert.That(DataVersion.FromClient("\"abc").ToString(), Is.EqualTo("\"abc"));
    }


    [Test]
    public void The_tag_for_a_known_term_does_not_move()
    {
        Assert.That(DataVersion.Compose([("total", 2L)]).ToString(), Is.EqualTo("f9a81beb-bf45-49cd-5e8c-468453f2c2f8"));
    }

    [Test]
    public void The_tag_for_a_known_page_does_not_move()
    {
        Assert.That(Page(("a", Noon, "Unresolved"), ("b", Noon, "Archived")).ToString(),
            Is.EqualTo("e8be8f59-107e-568e-6925-10cf61541b5c"));
    }

    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
}
