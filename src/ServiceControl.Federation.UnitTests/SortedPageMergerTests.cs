namespace ServiceControl.Federation.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Aggregation;
using NUnit.Framework;

[TestFixture]
class SortedPageMergerTests
{
    [Test]
    public void Merged_pages_match_a_global_sort([Values(1, 2, 3, 7)] int page, [Values(5, 10)] int pageSize, [Range(0, 9)] int seed)
    {
        var random = new Random(seed);
        var members = Enumerable.Range(0, random.Next(1, 5))
            .Select(member => Enumerable.Range(0, random.Next(0, 40))
                .Select(index => Row($"{member}-{index}", DateTime.UnixEpoch.AddMinutes(random.Next(0, 500))))
                .ToList())
            .ToList();

        var order = RowOrder.FailedMessages("time_of_failure", "desc");
        var depth = page * pageSize;

        // What each member returns for page=1&per_page=depth
        var memberPages = members
            .Select(rows => (IReadOnlyList<JsonNode>)[.. rows.OrderBy(r => r, order).Take(depth)])
            .ToList();

        var merged = SortedPageMerger.Merge(memberPages, order, depth - pageSize, pageSize);
        var expected = members.SelectMany(rows => rows).OrderBy(r => r, order).Skip(depth - pageSize).Take(pageSize);

        Assert.That(merged.Select(Id), Is.EqualTo(expected.Select(Id)));
    }

    [Test]
    public void Duplicates_keep_the_earliest_member()
    {
        var lead = Row("lead", DateTime.UnixEpoch, endpoint: "Sales", messageId: "m1");
        var other = Row("other", DateTime.UnixEpoch.AddMinutes(1), endpoint: "Sales", messageId: "m1");

        var merged = SortedPageMerger.Merge(
            [[lead], [other]],
            RowOrder.Messages("time_sent", "desc"),
            0,
            10,
            row => $"{JsonValues.Text(row, "receiving_endpoint.name")}-{JsonValues.Text(row, "message_id")}");

        Assert.That(merged.Select(Id), Is.EqualTo(new[] { "lead" }));
    }

    [Test]
    public void Durations_sort_by_value_not_text()
    {
        JsonNode Timed(string id, string criticalTime) => new JsonObject { ["id"] = id, ["critical_time"] = criticalTime };

        var merged = SortedPageMerger.Merge(
            [[Timed("long", "1.00:00:00")], [Timed("short", "09:00:00")]],
            RowOrder.Messages("critical_time", "asc"),
            0,
            10);

        Assert.That(merged.Select(Id), Is.EqualTo(new[] { "short", "long" }));
    }

    static JsonNode Row(string id, DateTime time, string endpoint = "Endpoint", string? messageId = null) => new JsonObject
    {
        ["id"] = id,
        ["message_id"] = messageId ?? id,
        ["time_of_failure"] = time.ToString("O"),
        ["time_sent"] = time.ToString("O"),
        ["receiving_endpoint"] = new JsonObject { ["name"] = endpoint }
    };

    static string? Id(JsonNode row) => JsonValues.Text(row, "id");
}
