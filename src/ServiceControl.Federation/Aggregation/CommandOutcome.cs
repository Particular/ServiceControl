namespace ServiceControl.Federation.Aggregation;

using System.Collections.Generic;
using System.Linq;
using Members;

/// <summary>
/// Picks the response a broadcast or partitioned command relays to the caller.
/// </summary>
public static class CommandOutcome
{
    /// <summary>
    /// The member response to relay, or null when no member answered at all.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Any success, with the rest 404 or failed: the success. A 404 means the member does not hold
    /// the resource, which is expected when a command reaches every member.</item>
    /// <item>Any 4xx other than 404 and no success: that client error, so its reason reaches the caller.</item>
    /// <item>All 404: a 404.</item>
    /// <item>Otherwise: the first failure.</item>
    /// </list>
    /// Among successes, 202 wins over 200 because the command is still in progress somewhere.
    /// </remarks>
    public static MemberResponse? Choose(IReadOnlyList<MemberResponse> responses)
    {
        var successes = responses.Where(r => r.IsSuccess).ToList();
        if (successes.Count > 0)
        {
            return successes.FirstOrDefault(r => r.Status == 202) ?? successes[0];
        }

        var clientError = responses.FirstOrDefault(r => r.Failure == null && r.Status is >= 400 and < 500 and not 404);
        if (clientError != null)
        {
            return clientError;
        }

        var notFound = responses.FirstOrDefault(r => r.IsNotFound);
        if (notFound != null && responses.All(r => r.IsNotFound))
        {
            return notFound;
        }

        return responses.FirstOrDefault(r => r.Failure != null) ?? notFound;
    }
}
