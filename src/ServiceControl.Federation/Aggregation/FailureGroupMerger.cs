namespace ServiceControl.Federation.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Merges failure groups (<c>GroupOperation</c> rows) by id. A group id is
/// <c>DeterministicGuid.MakeId(classifier, title)</c>, so the same group has the same id on every
/// member and the merged row describes the group across all of them.
/// </summary>
public static class FailureGroupMerger
{
    /// <summary>
    /// The maximum number of groups a member returns, kept for the merged list too.
    /// </summary>
    public const int MaxGroups = 200;

    /// <param name="memberGroups">One list per member, lead member first.</param>
    public static JsonArray MergeLists(IReadOnlyList<IReadOnlyList<JsonNode>> memberGroups)
    {
        var byId = new Dictionary<string, List<JsonNode>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var groups in memberGroups)
        {
            foreach (var group in groups)
            {
                var id = JsonValues.Text(group, "id");
                if (id == null)
                {
                    continue;
                }

                if (!byId.TryGetValue(id, out var instances))
                {
                    instances = [];
                    byId[id] = instances;
                    order.Add(id);
                }

                instances.Add(group);
            }
        }

        var merged = order
            .Select(id => Merge(byId[id]))
            .OrderByDescending(group => JsonValues.TimeTicks(group, "last") ?? long.MinValue)
            .Take(MaxGroups);

        return [.. merged];
    }

    /// <summary>
    /// Combines the views several members hold of one group.
    /// </summary>
    public static JsonObject Merge(IReadOnlyList<JsonNode> instances)
    {
        var result = instances[0].DeepClone().AsObject();
        if (instances.Count == 1)
        {
            return result;
        }

        result["count"] = instances.Sum(i => (long)(JsonValues.Number(i, "count") ?? 0));
        SetTimeExtreme(result, instances, "first", min: true);
        SetTimeExtreme(result, instances, "last", min: false);
        result["comment"] = MergeComment(instances);
        MergeOperation(result, instances);

        return result;
    }

    static string? MergeComment(IReadOnlyList<JsonNode> instances)
    {
        var comments = instances
            .Select(i => JsonValues.Text(i, "comment"))
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        // Differing comments can only come from edits made directly against a member; the proxy
        // broadcasts comment edits. The lead member's comment wins because it is listed first.
        return comments.FirstOrDefault();
    }

    static void MergeOperation(JsonObject result, IReadOnlyList<JsonNode> instances)
    {
        var participating = instances.Where(i => StatusRank(JsonValues.Text(i, "operation_status")) > 0).ToList();
        if (participating.Count == 0)
        {
            return;
        }

        var status = participating
            .Select(i => JsonValues.Text(i, "operation_status"))
            .OrderByDescending(StatusRank)
            .First();
        result["operation_status"] = status;

        var allComplete = participating.All(i => StatusRank(JsonValues.Text(i, "operation_status")) == CompletedRank);

        var weights = participating.Select(Weight).ToList();
        var totalWeight = weights.Sum();
        var progress = totalWeight == 0
            ? participating.Average(i => JsonValues.Number(i, "operation_progress") ?? 0)
            : participating.Select((i, index) => (JsonValues.Number(i, "operation_progress") ?? 0) * weights[index]).Sum() / totalWeight;
        result["operation_progress"] = progress;

        result["operation_remaining_count"] = SumOrNull(participating, "operation_remaining_count");
        result["operation_messages_completed_count"] = SumOrNull(participating, "operation_messages_completed_count");

        var failed = participating.Select(i => JsonValues.Bool(i, "operation_failed")).ToList();
        result["operation_failed"] = failed.All(f => f == null) ? null : failed.Any(f => f == true);

        result["need_user_acknowledgement"] = instances.Any(i => JsonValues.Bool(i, "need_user_acknowledgement") == true);

        SetTimeExtreme(result, participating, "operation_start_time", min: true);
        if (allComplete)
        {
            SetTimeExtreme(result, participating, "operation_completion_time", min: false);
        }
        else
        {
            result["operation_completion_time"] = null;
        }

        SetTimeExtreme(result, instances, "last_operation_completion_time", min: false);
    }

    static double Weight(JsonNode group)
    {
        var remaining = JsonValues.Number(group, "operation_remaining_count");
        var completed = JsonValues.Number(group, "operation_messages_completed_count");
        if (remaining != null || completed != null)
        {
            return (remaining ?? 0) + (completed ?? 0);
        }

        return JsonValues.Number(group, "count") ?? 0;
    }

    static long? SumOrNull(IReadOnlyList<JsonNode> instances, string field)
    {
        var values = instances.Select(i => JsonValues.Number(i, field)).Where(v => v != null).ToList();
        return values.Count == 0 ? null : values.Sum(v => (long)v!.Value);
    }

    static void SetTimeExtreme(JsonObject result, IReadOnlyList<JsonNode> instances, string field, bool min)
    {
        var candidates = instances
            .Select(i => (Node: JsonValues.At(i, field), Ticks: JsonValues.TimeTicks(i, field)))
            .Where(c => c.Ticks != null)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var winner = min ? candidates.MinBy(c => c.Ticks) : candidates.MaxBy(c => c.Ticks);
        result[field] = winner.Node?.DeepClone();
    }

    /// <summary>
    /// How active an operation state is. The merged state is the most active one any member reports,
    /// so a group shows as in progress until every member has finished.
    /// </summary>
    public static int StatusRank(string? status) => status switch
    {
        null or "" or "None" => 0,
        "Completed" or "ArchiveCompleted" => CompletedRank,
        "Waiting" => 3,
        "Forwarding" or "ArchiveProgressing" or "ArchiveFinalizing" => 4,
        "Preparing" or "ArchiveStarted" => 5,
        _ => 3
    };

    const int CompletedRank = 1;
}
