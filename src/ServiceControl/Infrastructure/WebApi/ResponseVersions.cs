namespace ServiceControl.Infrastructure.WebApi;

using System.Collections.Generic;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Versions for responses a controller assembles itself, rather than getting from a store. Each takes the
/// total as well as the rows: it carries whatever the response says about the whole set, and it keeps an
/// empty list cacheable, because with no rows and no summary there would be no terms to compose and so no
/// validator at all.
/// </summary>
static class ResponseVersions
{
    internal static DataVersion VersionOf(IReadOnlyList<GroupOperation> page, int total) =>
        DataVersion.OverRows(
            [("groups", total)],
            page,
            group => [group.Id, group.Title, group.Type, group.Count, group.Comment, group.First, group.Last,
                group.OperationStatus, group.OperationFailed, group.OperationProgress, group.OperationMessagesCompletedCount,
                group.OperationRemainingCount, group.OperationStartTime, group.OperationCompletionTime, group.NeedUserAcknowledgement]);

    internal static DataVersion VersionOf(IReadOnlyList<MessageRedirect> page, int total) =>
        DataVersion.OverRows(
            [("redirects", total)],
            page,
            redirect => [redirect.MessageRedirectId, redirect.ToPhysicalAddress, redirect.LastModified]);
}
