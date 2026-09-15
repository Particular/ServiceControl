#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationContractShapeTests
{
    [Test]
    public void MigrationCategory_carries_no_fact_about_where_a_store_keeps_its_rows()
    {
        var properties = typeof(MigrationCategory).GetProperties().Select(p => p.Name);

        Assert.That(properties, Is.EquivalentTo(new[] { "Id", "Kind", "CarriesBodies", "Order", "MustFollow" }),
            "a source database or target table belongs in that store's own adapter, where a different store pair can map it differently");
    }

    [Test]
    public void Abandoned_is_a_state_of_its_own_and_not_a_kind_of_complete()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MigrationCategoryState.Abandoned, Is.Not.EqualTo(MigrationCategoryState.Complete));
            Assert.That(MigrationCategoryState.Abandoned, Is.Not.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(MigrationCategoryState.Abandoned, Is.Not.EqualTo(MigrationCategoryState.Halted));
        }
    }

    [Test]
    public void Category_states_keep_the_integers_stored_checkpoints_already_hold()
    {
        var stored = System.Enum.GetValues<MigrationCategoryState>().ToDictionary(state => state.ToString(), state => (int)state);

        Assert.That(stored, Is.EqualTo(new Dictionary<string, int>
        {
            ["NotStarted"] = 0,
            ["InProgress"] = 1,
            ["Complete"] = 2,
            ["CompleteWithErrors"] = 3,
            ["Halted"] = 4,
            ["Abandoned"] = 5
        }), "checkpoints store State as an integer, so a reordered or inserted member silently changes what every saved checkpoint means");
    }

    [Test]
    public void MigrationRow_body_defaults_to_null()
    {
        var row = new MigrationRow("id-1", new object(), new Dictionary<string, object?>());

        Assert.That(row.Body, Is.Null);
    }

    [Test]
    public void MigrationRow_can_carry_a_body()
    {
        var body = new MigrationBody(new byte[] { 1, 2, 3 }, "text/plain");
        var row = new MigrationRow("id-1", new object(), new Dictionary<string, object?>(), body);

        Assert.That(row.Body, Is.EqualTo(body));
    }

    [Test]
    public void MigrationBatch_groups_rows_under_one_cursor()
    {
        var rows = new[] { new MigrationRow("id-1", new object(), new Dictionary<string, object?>()) };
        var batch = new MigrationBatch(rows, Cursor: "id-1");

        Assert.That(batch.Cursor, Is.EqualTo("id-1"));
    }

    [Test]
    public void MigrationCheckpoint_starts_with_no_cursor_and_zero_counts()
    {
        var checkpoint = new MigrationCheckpoint(
            CategoryId: "EndpointSettings",
            Selected: false,
            State: MigrationCategoryState.NotStarted,
            Cursor: null,
            CopiedCount: 0,
            SkippedCount: 0,
            SourceTotal: null,
            SkipReasons: null,
            StartedAt: null,
            LastProgressAt: null,
            CompletedAt: null,
            AbandonedAt: null,
            LastError: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.Cursor, Is.Null);
            Assert.That(checkpoint.CopiedCount, Is.Zero);
            Assert.That(checkpoint.AlreadyPresentCount, Is.Zero);
        }
    }
}
