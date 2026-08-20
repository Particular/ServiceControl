namespace ServiceControl.Infrastructure.WebApi;

using System.Collections.Generic;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Versions for responses a controller assembles itself, rather than getting from a store.
/// </summary>
static class ResponseVersions
{
    internal static DataVersion VersionOf(GroupOperation[] groups) =>
        DataVersion.OverRows([("groups", groups.Length)], groups,
            group => [group.Id, group.Title, group.Type, group.Count, group.Comment, group.First, group.Last,
                group.OperationStatus, group.OperationFailed, group.OperationProgress, group.OperationMessagesCompletedCount,
                group.OperationRemainingCount, group.OperationStartTime, group.OperationCompletionTime, group.NeedUserAcknowledgement]);

    // FromPhysicalAddress needs no field of its own: MessageRedirectId is a deterministic hash of it.
    internal static DataVersion VersionOf(IReadOnlyList<MessageRedirect> redirects) =>
        DataVersion.OverRows([("redirects", redirects.Count)], redirects,
            redirect => [redirect.MessageRedirectId, redirect.ToPhysicalAddress, redirect.LastModified]);
}
