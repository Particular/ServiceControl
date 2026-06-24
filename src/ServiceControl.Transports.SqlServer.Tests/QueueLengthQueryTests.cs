namespace ServiceControl.Transport.Tests;

using System;
using System.Linq;
using NUnit.Framework;
using Transports.SqlServer;

[TestFixture]
class QueueLengthQueryTests
{
    [Test]
    public void BuildBulkLengthQuery_emits_a_single_catalog_view_query_for_all_tables()
    {
        var tables = new[]
        {
            SqlTable.Parse("Sales", "dbo"),
            SqlTable.Parse("Billing", "dbo"),
        };

        var query = SqlTable.BuildBulkLengthQuery(catalog: null, tables);

        // One statement, read from the catalog views — no per-table IF EXISTS, no scan of the queue tables.
        Assert.That(query, Does.Contain("sys.partitions"));
        Assert.That(query, Does.Contain("p.index_id IN (0, 1)"));
        Assert.That(query, Does.Not.Contain("INFORMATION_SCHEMA"));
        Assert.That(query, Does.Not.Contain("RowVersion"));

        // Both tracked tables are covered by the single query.
        Assert.That(query, Does.Contain("t.name = 'Sales'"));
        Assert.That(query, Does.Contain("t.name = 'Billing'"));

        // Exactly one SELECT that returns rows (the leading SET is not a result-producing statement).
        Assert.That(System.Text.RegularExpressions.Regex.Matches(query, "SELECT").Count, Is.EqualTo(1));
    }

    [Test]
    public void BuildBulkLengthQuery_prefixes_the_catalog_when_supplied()
    {
        var tables = new[] { SqlTable.Parse("Sales@dbo@MyCatalog", "dbo") };

        var query = SqlTable.BuildBulkLengthQuery(catalog: "MyCatalog", tables);

        Assert.That(query, Does.Contain("[MyCatalog].sys.partitions"));
        Assert.That(query, Does.Contain("[MyCatalog].sys.tables"));
        Assert.That(query, Does.Contain("[MyCatalog].sys.schemas"));
    }

    [Test]
    public void RemoveQueueLengthQueryDelayInterval_parses_and_strips_the_custom_part()
    {
        const string connectionString = "Data Source=.;Initial Catalog=nsb;Integrated Security=true;QueueLengthQueryDelayInterval=1000";

        var cleaned = connectionString.RemoveQueueLengthQueryDelayInterval(out var interval);

        Assert.That(interval, Is.EqualTo(TimeSpan.FromSeconds(1)));
        // The custom key must be stripped so SqlConnection never sees the unknown keyword.
        Assert.That(cleaned, Does.Not.Contain("QueueLengthQueryDelayInterval").IgnoreCase);
        Assert.That(cleaned, Does.Contain("Initial Catalog=nsb").IgnoreCase);
    }

    [Test]
    public void RemoveQueueLengthQueryDelayInterval_defaults_to_null_when_absent()
    {
        const string connectionString = "Data Source=.;Initial Catalog=nsb;Integrated Security=true";

        connectionString.RemoveQueueLengthQueryDelayInterval(out var interval);

        Assert.That(interval, Is.Null);
    }

    [Test]
    public void RemoveQueueLengthQueryDelayInterval_throws_on_non_numeric_value()
    {
        const string connectionString = "Data Source=.;Initial Catalog=nsb;QueueLengthQueryDelayInterval=soon";

        Assert.That(() => connectionString.RemoveQueueLengthQueryDelayInterval(out _),
            Throws.Exception.With.Message.Contains("QueueLengthQueryDelayInterval"));
    }

    [Test]
    public void RemoveQueueLengthQueryMaxDelayInterval_parses_and_strips_the_custom_part()
    {
        const string connectionString = "Data Source=.;Initial Catalog=nsb;QueueLengthQueryMaxDelayInterval=60000";

        var cleaned = connectionString.RemoveQueueLengthQueryMaxDelayInterval(out var interval);

        Assert.That(interval, Is.EqualTo(TimeSpan.FromSeconds(60)));
        Assert.That(cleaned, Does.Not.Contain("QueueLengthQueryMaxDelayInterval").IgnoreCase);
    }

    static readonly TimeSpan Base = TimeSpan.FromMilliseconds(200);
    static readonly TimeSpan Max = TimeSpan.FromSeconds(60);

    [Test]
    public void NextDelay_snaps_back_to_base_when_any_queue_has_work()
    {
        // Even fully backed off, a single non-empty queue returns to full speed immediately.
        Assert.That(QueueLengthProvider.NextDelay(Max, Base, Max, maxObservedLength: 1), Is.EqualTo(Base));
    }

    [Test]
    public void NextDelay_doubles_while_idle()
    {
        Assert.That(QueueLengthProvider.NextDelay(Base, Base, Max, maxObservedLength: 0),
            Is.EqualTo(TimeSpan.FromMilliseconds(400)));
    }

    [Test]
    public void NextDelay_caps_at_max_while_idle()
    {
        Assert.That(QueueLengthProvider.NextDelay(TimeSpan.FromSeconds(40), Base, Max, maxObservedLength: 0),
            Is.EqualTo(Max));
    }

    [Test]
    public void NextDelay_never_drops_below_base()
    {
        Assert.That(QueueLengthProvider.NextDelay(TimeSpan.FromMilliseconds(50), Base, Max, maxObservedLength: 0),
            Is.EqualTo(Base));
    }

    [Test]
    public void NextDelay_with_max_equal_to_base_disables_backoff()
    {
        // Operator did not opt in (max == base) -> cadence is constant at base, even while idle.
        Assert.That(QueueLengthProvider.NextDelay(Base, Base, Base, maxObservedLength: 0), Is.EqualTo(Base));
    }
}
