using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Versions for responses built in a controller.
/// </summary>
static class ResponseVersions
{
    internal static DataVersion VersionOf(GroupOperation[] groups) =>
        DataVersion.Compose(
            ("groups", groups.Length),
            ("state", string.Join("|", groups.Select(group => FormattableString.Invariant(
                $"{group.Id}.{group.Title}.{group.Type}.{group.Count}.{group.Comment}.{group.First?.Ticks}.{group.Last?.Ticks}.{group.OperationStatus}.{group.OperationFailed}.{group.OperationProgress}.{group.OperationMessagesCompletedCount}.{group.OperationRemainingCount}.{group.OperationStartTime?.Ticks}.{group.OperationCompletionTime?.Ticks}.{group.NeedUserAcknowledgement}")))));

    internal static DataVersion VersionOf(IReadOnlyList<MessageRedirect> redirects) =>
        DataVersion.Compose(
            ("redirects", redirects.Count),
            ("state", string.Join("|", redirects.Select(redirect => FormattableString.Invariant(
                $"{redirect.MessageRedirectId}.{redirect.ToPhysicalAddress}.{redirect.LastModified.Ticks}")))));
}
