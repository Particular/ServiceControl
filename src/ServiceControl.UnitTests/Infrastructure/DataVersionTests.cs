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

        Assert.Multiple(() =>
        {
            Assert.That(DataVersion.None.Matches(real), Is.False);
            Assert.That(real.Matches(DataVersion.None), Is.False);
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(DataVersion.FromToken(null).HasValue, Is.False);
            Assert.That(DataVersion.FromToken(string.Empty).HasValue, Is.False);
        });
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
    public void Combine_does_not_depend_on_the_order_instances_answered_in()
    {
        var a = DataVersion.FromToken("a");
        var b = DataVersion.FromToken("b");

        Assert.That(DataVersion.Combine([a, b]).Matches(DataVersion.Combine([b, a])), Is.True);
    }

    [Test]
    public void Combine_differs_from_every_instance_version_it_covers()
    {
        var a = DataVersion.FromToken("a");
        var b = DataVersion.FromToken("b");

        var combined = DataVersion.Combine([a, b]);

        Assert.Multiple(() =>
        {
            Assert.That(combined.Matches(a), Is.False);
            Assert.That(combined.Matches(b), Is.False);
        });
    }

    [Test]
    public void Combine_is_absent_when_any_instance_has_no_version()
    {
        var combined = DataVersion.Combine([DataVersion.FromToken("a"), DataVersion.None]);

        Assert.That(combined.HasValue, Is.False,
            "a composite that ignored an instance would stop reporting that instance's changes");
    }

    [Test]
    public void Combine_of_nothing_is_absent()
    {
        Assert.That(DataVersion.Combine([]).HasValue, Is.False);
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
    public void Only_FromContent_promises_byte_equivalence()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DataVersion.FromContent("cv-1").IsStrong, Is.True);
            Assert.That(DataVersion.FromToken("cv-1").IsStrong, Is.False);
            Assert.That(DataVersion.FromToken(1L).IsStrong, Is.False);
            Assert.That(DataVersion.FromClient("\"cv-1\"").IsStrong, Is.False);
            Assert.That(DataVersion.None.IsStrong, Is.False);
        });
    }

    [Test]
    public void Composing_and_combining_are_never_exact()
    {
        var exact = DataVersion.FromContent("cv-1");

        Assert.Multiple(() =>
        {
            Assert.That(DataVersion.Compose(("total", 3L)).IsStrong, Is.False,
                "a hash over aggregates cannot promise the bytes are identical");
            Assert.That(DataVersion.Combine([exact, exact]).IsStrong, Is.False,
                "a composite across instances is an approximation whatever went into it");
        });
    }

    [Test]
    public void Matching_ignores_the_marking()
    {
        // RFC 9110 requires the weak comparison, which ignores the marking. Anything coming back through
        // FromClient has lost its marking anyway, so this is the normal case and not an edge one.
        Assert.That(DataVersion.FromContent("cv-1").Matches(DataVersion.FromClient("W/\"cv-1\"")), Is.True);
    }

    [Test]
    public void Equality_does_not_ignore_the_marking()
    {
        Assert.That(DataVersion.FromContent("cv-1").Equals(DataVersion.FromToken("cv-1")), Is.False,
            "Equals is ordinary value equality over everything the struct holds, which is why it must never decide not-modified");
    }
}
