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
    public void MigrationCheckpoint_AlreadyPresentCount_defaults_to_zero()
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

        Assert.That(checkpoint.AlreadyPresentCount, Is.Zero);
    }
}
