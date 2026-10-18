namespace ServiceControl.Federation.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Merges sorted row lists from several members into one page.
/// </summary>
/// <remarks>
/// Page <c>p</c> of size <c>s</c> holds rows <c>(p-1)·s … p·s-1</c> of the global order, and no member
/// can contribute more than its own first <c>p·s</c> rows to them. Asking each member for its first
/// <c>p·s</c> rows and merging is therefore exact. ServiceControl's own scatter-gather instead asks every
/// instance for its page <c>p</c>, which is only exact for page 1.
/// </remarks>
public static class SortedPageMerger
{
    /// <param name="memberRows">One list per member, each already sorted by <paramref name="order"/>, lead member first.</param>
    /// <param name="order">The order the members sorted by.</param>
    /// <param name="skip">Rows of the merged order to skip.</param>
    /// <param name="take">Rows to return.</param>
    /// <param name="deduplicationKey">Rows with an equal key are kept once; the earliest member wins.</param>
    public static List<JsonNode> Merge(
        IReadOnlyList<IReadOnlyList<JsonNode>> memberRows,
        IComparer<JsonNode?> order,
        int skip,
        int take,
        Func<JsonNode, string?>? deduplicationKey = null)
    {
        var seen = deduplicationKey == null ? null : new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<JsonNode>();

        foreach (var rows in memberRows)
        {
            foreach (var row in rows)
            {
                var key = deduplicationKey?.Invoke(row);
                if (seen != null && key != null && !seen.Add(key))
                {
                    continue;
                }

                candidates.Add(row);
            }
        }

        // OrderBy is stable, so rows that compare equal keep member order.
        return [.. candidates.OrderBy(row => row, order).Skip(skip).Take(take)];
    }
}
