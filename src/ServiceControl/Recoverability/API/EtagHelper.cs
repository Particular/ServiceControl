using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Versions for responses built in a controller, where there are no rows to count and no timestamp to read.
/// Every property the response shows has to be named below, or a client keeps a page that has since changed.
/// Invariant, or the double renders as 0,01 on some machines. Ticks, to match what DataVersion does.
/// </summary>
static class EtagHelper
{
    /// <summary>
    /// All properties. OperationProgress arrives already rounded to two decimals, so on a big retry a
    /// message completing moves only the two counters.
    /// </summary>
    internal static DataVersion VersionOf(GroupOperation[] groups) =>
        DataVersion.Compose(
            ("groups", groups.Length),
            ("state", string.Join("|", groups.Select(group => FormattableString.Invariant(
                $"{group.Id}.{group.Title}.{group.Type}.{group.Count}.{group.Comment}.{group.First?.Ticks}.{group.Last?.Ticks}.{group.OperationStatus}.{group.OperationFailed}.{group.OperationProgress}.{group.OperationMessagesCompletedCount}.{group.OperationRemainingCount}.{group.OperationStartTime?.Ticks}.{group.OperationCompletionTime?.Ticks}.{group.NeedUserAcknowledgement}")))));

    /// <summary>
    /// FromPhysicalAddress is not named because MessageRedirectId is derived from it.
    /// </summary>
    internal static DataVersion VersionOf(IReadOnlyList<MessageRedirect> redirects) =>
        DataVersion.Compose(
            ("redirects", redirects.Count),
            ("state", string.Join("|", redirects.Select(redirect => FormattableString.Invariant(
                $"{redirect.MessageRedirectId}.{redirect.ToPhysicalAddress}.{redirect.LastModified.Ticks}")))));
}
