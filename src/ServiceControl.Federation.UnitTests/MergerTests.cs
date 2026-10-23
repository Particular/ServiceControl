namespace ServiceControl.Federation.UnitTests;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Aggregation;
using NUnit.Framework;
using Routing;

[TestFixture]
class FailureGroupMergerTests
{
    [Test]
    public void Groups_with_the_same_id_are_combined()
    {
        var merged = FailureGroupMerger.MergeLists(
        [
            [Group("g1", 3, "2024-01-01T00:00:00Z", "2024-01-05T00:00:00Z")],
            [Group("g1", 4, "2023-12-01T00:00:00Z", "2024-01-02T00:00:00Z"), Group("g2", 1, "2024-02-01T00:00:00Z", "2024-02-01T00:00:00Z")]
        ]);

        Assert.That(merged, Has.Count.EqualTo(2));
        var g1 = merged.Single(g => JsonValues.Text(g, "id") == "g1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonValues.Number(g1, "count"), Is.EqualTo(7));
            Assert.That(JsonValues.Text(g1, "first"), Is.EqualTo("2023-12-01T00:00:00Z"));
            Assert.That(JsonValues.Text(g1, "last"), Is.EqualTo("2024-01-05T00:00:00Z"));
            Assert.That(JsonValues.Text(merged[0], "id"), Is.EqualTo("g2"), "ordered by last, newest first");
        }
    }

    [Test]
    public void Operation_state_reflects_the_most_active_member()
    {
        var done = Group("g1", 0, null, null);
        done["operation_status"] = "Completed";
        done["operation_progress"] = 1.0;
        done["operation_remaining_count"] = 0;
        done["operation_messages_completed_count"] = 30;
        done["operation_completion_time"] = "2024-01-01T00:00:00Z";

        var running = Group("g1", 10, null, null);
        running["operation_status"] = "Forwarding";
        running["operation_progress"] = 0.0;
        running["operation_remaining_count"] = 10;
        running["operation_messages_completed_count"] = 0;

        var idle = Group("g1", 5, null, null);
        idle["operation_status"] = "None";

        var merged = FailureGroupMerger.Merge([done, running, idle]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonValues.Text(merged, "operation_status"), Is.EqualTo("Forwarding"));
            Assert.That(JsonValues.Number(merged, "operation_progress"), Is.EqualTo(0.75));
            Assert.That(JsonValues.Number(merged, "operation_remaining_count"), Is.EqualTo(10));
            Assert.That(merged["operation_completion_time"], Is.Null, "not complete until every member is");
        }
    }

    static JsonObject Group(string id, int count, string? first, string? last) => new()
    {
        ["id"] = id,
        ["title"] = "System.Exception",
        ["type"] = "Exception Type and Stack Trace",
        ["count"] = count,
        ["first"] = first,
        ["last"] = last,
        ["operation_status"] = "None"
    };
}

[TestFixture]
class MonitoredEndpointMergerTests
{
    [Test]
    public void Endpoints_with_the_same_name_are_combined()
    {
        var merged = MonitoredEndpointMerger.MergeList(
        [
            [Endpoint("Sales", stale: false, throughput: [10, 10], processingTime: [100, 100])],
            [Endpoint("Sales", stale: true, throughput: [30, 0], processingTime: [200, 0]), Endpoint("Billing", stale: true, throughput: [1, 1], processingTime: [1, 1])]
        ]);

        Assert.That(merged, Has.Count.EqualTo(2));
        var sales = merged[0]!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonValues.Bool(sales, "isStale"), Is.False);
            Assert.That(Points(sales, "throughput"), Is.EqualTo(new[] { 40.0, 10.0 }));
            Assert.That(Points(sales, "processingTime"), Is.EqualTo(new[] { 175.0, 100.0 }), "weighted by throughput");
            Assert.That((JsonValues.At(sales, "endpointInstanceIds") as JsonArray)!, Has.Count.EqualTo(2));
        }
    }

    static IEnumerable<double?> Points(JsonNode endpoint, string metric) =>
        (JsonValues.At(endpoint, $"metrics.{metric}.points") as JsonArray)!.Select(JsonValues.AsNumber);

    static JsonObject Endpoint(string name, bool stale, double[] throughput, double[] processingTime) => new()
    {
        ["name"] = name,
        ["isStale"] = stale,
        ["endpointInstanceIds"] = new JsonArray($"{name}-{stale}"),
        ["metrics"] = new JsonObject
        {
            ["throughput"] = new JsonObject { ["average"] = throughput.Average(), ["points"] = new JsonArray([.. throughput.Select(p => (JsonNode)p)]) },
            ["processingTime"] = new JsonObject { ["average"] = processingTime.Average(), ["points"] = new JsonArray([.. processingTime.Select(p => (JsonNode)p)]) }
        }
    };
}

[TestFixture]
class InstanceIdCodecTests
{
    [TestCase("/messages/1/body?instance_id=aHR0cDovL2E.", "a", "/messages/1/body?instance_id=a~aHR0cDovL2E.")]
    [TestCase("/messages/1/body", "a", "/messages/1/body?instance_id=a~")]
    [TestCase("/messages/1/body?x=1", "b", "/messages/1/body?x=1&instance_id=b~")]
    public void Body_urls_carry_the_member_key(string url, string member, string expected) =>
        Assert.That(InstanceIdCodec.RewriteBodyUrl(url, member), Is.EqualTo(expected));

    [Test]
    public void Encoded_ids_round_trip()
    {
        Assert.That(InstanceIdCodec.TryDecode(InstanceIdCodec.Encode("eu", "aHR0cDovL2E."), out var key, out var id), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(key, Is.EqualTo("eu"));
            Assert.That(id, Is.EqualTo("aHR0cDovL2E."));
        }

        Assert.That(InstanceIdCodec.TryDecode("aHR0cDovL2E.", out _, out _), Is.False, "a member's own id is not routable");
    }

    [Test]
    public void Trees_are_rewritten_in_place()
    {
        var row = new JsonObject
        {
            ["instance_id"] = "abc",
            ["body_url"] = "/messages/1/body?instance_id=abc",
            ["nested"] = new JsonArray(new JsonObject { ["body_url"] = "/messages/2/body" })
        };

        InstanceIdCodec.RewriteTree(row, "eu");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonValues.Text(row, "instance_id"), Is.EqualTo("eu~abc"));
            Assert.That(JsonValues.Text(row, "body_url"), Is.EqualTo("/messages/1/body?instance_id=eu~abc"));
            Assert.That(JsonValues.Text(row["nested"]![0], "body_url"), Is.EqualTo("/messages/2/body?instance_id=eu~"));
        }
    }
}

[TestFixture]
class VersionsTests
{
    [Test]
    public void The_lowest_version_wins() =>
        Assert.That(Versions.Lowest(["6.10.0", "6.9.1", null, "6.9.1-alpha.3+sha"]), Is.EqualTo("6.9.1"));
}
