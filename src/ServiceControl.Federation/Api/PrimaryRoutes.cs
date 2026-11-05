namespace ServiceControl.Federation.Api;

using System.Collections.Generic;
using Members;
using Routing;
using S = Strategy;

/// <summary>
/// Every primary (error) instance route, relative to <c>/api</c>, with the strategy that answers it.
/// The route coverage test compares this table with the primary's approved route list, so a new
/// ServiceControl route fails the build until it is classified here.
/// </summary>
public static class PrimaryRoutes
{
    public static readonly IReadOnlyList<FederatedRoute> All =
    [
        // Discovery, configuration and authorization
        Get("", S.Synthesised, PrimaryHandlers.Root) with { Anonymous = true },
        Get("configuration", S.Synthesised, PrimaryHandlers.Configuration) with { Anonymous = true },
        Get("instance-info", S.Synthesised, PrimaryHandlers.Configuration) with { Anonymous = true },
        Get("configuration/remotes", S.Union, PrimaryHandlers.Remotes) with { Anonymous = true },
        Get("authentication/configuration", S.Lead, (f, c) => f.Lead(c, Surface.Primary)) with { Anonymous = true },
        Get("connection", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Get("my/routes", S.Synthesised, PrimaryHandlers.MyRoutes),

        // License
        Get("license", S.Synthesised, PrimaryHandlers.License),
        Get("license/details", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Post("license/detailsUpload", S.Lead, (f, c) => f.LeadStreaming(c, Surface.Primary)),
        // Amends the details uploaded above, so it goes to the same member.
        Post("license/amendDetails", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),

        // Failed messages
        Get("errors", S.SortedPage, PrimaryHandlers.FailedMessages),
        Head("errors", S.Sum, (f, c) => f.SumCount(c, Surface.Primary)),
        Get("errors/summary", S.Sum, PrimaryHandlers.ErrorsSummary),
        Get("errors/{failedMessageId}", S.FirstHit, (f, c) => f.FirstHit(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Get("errors/last/{failedMessageId}", S.FirstHit, (f, c) => f.FirstHit(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Get("errors/queues/addresses", S.KeyMerge, PrimaryHandlers.QueueAddresses),
        Get("errors/groups/{classifier?}", S.KeyMerge, PrimaryHandlers.Groups),
        Get("endpoints/{endpointname}/errors", S.SortedPage, PrimaryHandlers.FailedMessages),
        Get("archive/groups/id/{groupId}", S.KeyMerge, PrimaryHandlers.GroupById),
        Post("errors/retry", S.RouteToOwner, (f, c) => f.Partitioned(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Post("errors/retry/all", S.Broadcast, Broadcast),
        Post("errors/{endpointName}/retry/all", S.Broadcast, Broadcast),
        Post("errors/queues/{queueAddress}/retry", S.Broadcast, Broadcast),
        Post("errors/{failedMessageId}/retry", S.RouteToOwner, PrimaryHandlers.RetryById),
        Route(["PATCH", "POST"], "errors/archive", S.RouteToOwner, (f, c) => f.Partitioned(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Route(["PATCH", "POST"], "errors/{messageId}/archive", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.FailedMessage, PrimaryHandlers.RouteValue(c, "messageId"))),
        Patch("errors/unarchive", S.RouteToOwner, (f, c) => f.Partitioned(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Patch("errors/{from}...{to}/unarchive", S.Broadcast, Broadcast),
        Get("edit/config", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Post("edit/{failedMessageId}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.FailedMessage, PrimaryHandlers.RouteValue(c, "failedMessageId"))),

        // Pending retries
        Post("pendingretries/retry", S.RouteToOwner, (f, c) => f.Partitioned(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Patch("pendingretries/resolve", S.RouteToOwner, (f, c) => f.Partitioned(c, Surface.Primary, OwnedRecord.FailedMessage)),
        Post("pendingretries/queues/retry", S.Broadcast, Broadcast),
        Patch("pendingretries/queues/resolve", S.Broadcast, Broadcast),

        // Failure groups
        Get("recoverability/classifiers", S.Union, PrimaryHandlers.Classifiers),
        Get("recoverability/groups/{classifier?}", S.KeyMerge, PrimaryHandlers.Groups),
        Get("recoverability/groups/id/{groupId}", S.KeyMerge, PrimaryHandlers.GroupById),
        Get("recoverability/groups/{groupId}/errors", S.SortedPage, PrimaryHandlers.FailedMessages),
        Head("recoverability/groups/{groupId}/errors", S.Sum, (f, c) => f.SumCount(c, Surface.Primary)),
        Post("recoverability/groups/{groupId}/errors/retry", S.Broadcast, Broadcast),
        Post("recoverability/groups/{groupId}/errors/archive", S.Broadcast, Broadcast),
        Post("recoverability/groups/{groupId}/errors/unarchive", S.Broadcast, Broadcast),
        Post("recoverability/groups/{groupId}/comment", S.Broadcast, Broadcast),
        Delete("recoverability/groups/{groupId}/comment", S.Broadcast, Broadcast),
        Get("recoverability/history", S.Union, PrimaryHandlers.History),
        Delete("recoverability/unacknowledgedgroups/{groupId}", S.Broadcast, Broadcast),

        // Audit and composite message views
        Get("messages", S.SortedPage, PrimaryHandlers.Messages),
        Get("messages/search", S.SortedPage, PrimaryHandlers.Messages),
        Get("messages/search/{keyword}", S.SortedPage, PrimaryHandlers.Messages),
        Get("messages/{id}/body", S.RouteToOwner, PrimaryHandlers.Body),
        Get("messages2", S.TopN, PrimaryHandlers.Messages2),
        Get("endpoints/{endpoint}/messages", S.SortedPage, PrimaryHandlers.Messages),
        Get("endpoints/{endpoint}/messages/search", S.SortedPage, PrimaryHandlers.Messages),
        Get("endpoints/{endpoint}/messages/search/{keyword}", S.SortedPage, PrimaryHandlers.Messages),
        Get("endpoints/{endpoint}/audit-count", S.KeyMerge, PrimaryHandlers.AuditCounts),
        Get("conversations/{conversationId}", S.SortedPage, PrimaryHandlers.Messages),
        Get("sagas/{id}", S.KeyMerge, PrimaryHandlers.Saga),

        // Endpoints and heartbeats
        Get("endpoints", S.Union, PrimaryHandlers.Endpoints),
        Options("endpoints", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Get("endpoints/known", S.Union, PrimaryHandlers.KnownEndpoints),
        Patch("endpoints/{endpointId}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.Endpoint, PrimaryHandlers.RouteValue(c, "endpointId"))),
        Delete("endpoints/{endpointId}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.Endpoint, PrimaryHandlers.RouteValue(c, "endpointId"))),
        Get("endpointssettings", S.KeyMerge, PrimaryHandlers.EndpointSettings),
        Patch("endpointssettings/{endpointName?}", S.Broadcast, Broadcast),
        Get("heartbeats/stats", S.Sum, PrimaryHandlers.SumNumbers),

        // Custom checks and event log
        Get("customchecks", S.SortedPage, PrimaryHandlers.CustomChecks),
        Delete("customchecks/{id}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.CustomCheck, PrimaryHandlers.RouteValue(c, "id"))),
        Get("eventlogitems", S.SortedPage, PrimaryHandlers.EventLog),

        // Retry redirects
        Get("redirects", S.SortedPage, PrimaryHandlers.Redirects),
        Head("redirect", S.Sum, (f, c) => f.SumCount(c, Surface.Primary)),
        Post("redirects", S.RouteToOwner, PrimaryHandlers.CreateRedirect),
        Put("redirects/{messageRedirectId}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.Redirect, PrimaryHandlers.RouteValue(c, "messageRedirectId"))),
        Delete("redirects/{messageRedirectId}", S.RouteToOwner, (f, c) => f.RouteToOwners(c, Surface.Primary, OwnedRecord.Redirect, PrimaryHandlers.RouteValue(c, "messageRedirectId"))),

        // Notifications
        Get("notifications/email", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Post("notifications/email", S.Broadcast, Broadcast),
        Post("notifications/email/toggle", S.Broadcast, Broadcast),
        Post("notifications/email/test", S.Broadcast, Broadcast),

        // Retention maintenance
        Post("maintenance/retention/purge", S.Broadcast, Broadcast),
        Get("maintenance/retention/purge/status", S.KeyMerge, PrimaryHandlers.RetentionStatus),

        // Licensing component (usage reporting)
        Get("licensing/endpoints", S.KeyMerge, LicensingHandlers.Endpoints),
        Post("licensing/endpoints/update", S.Broadcast, Broadcast),
        Get("licensing/report/available", S.Synthesised, LicensingHandlers.ReportAvailable),
        Get("licensing/report/file", S.Synthesised, LicensingHandlers.ReportFile),
        Get("licensing/settings/info", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Get("licensing/settings/test", S.Synthesised, LicensingHandlers.SettingsTest),
        Get("licensing/settings/masks", S.Lead, (f, c) => f.Lead(c, Surface.Primary)),
        Post("licensing/settings/masks/update", S.Broadcast, Broadcast),
    ];

    static System.Threading.Tasks.Task Broadcast(Federator federator, Microsoft.AspNetCore.Http.HttpContext context) => federator.Broadcast(context, Surface.Primary);

    static FederatedRoute Get(string template, string strategy, RouteHandler handler) => Route(["GET"], template, strategy, handler);

    static FederatedRoute Head(string template, string strategy, RouteHandler handler) => Route(["HEAD"], template, strategy, handler);

    static FederatedRoute Post(string template, string strategy, RouteHandler handler) => Route(["POST"], template, strategy, handler);

    static FederatedRoute Put(string template, string strategy, RouteHandler handler) => Route(["PUT"], template, strategy, handler);

    static FederatedRoute Patch(string template, string strategy, RouteHandler handler) => Route(["PATCH"], template, strategy, handler);

    static FederatedRoute Delete(string template, string strategy, RouteHandler handler) => Route(["DELETE"], template, strategy, handler);

    static FederatedRoute Options(string template, string strategy, RouteHandler handler) => Route(["OPTIONS"], template, strategy, handler);

    static FederatedRoute Route(string[] methods, string template, string strategy, RouteHandler handler) => new(Surface.Primary, methods, template, strategy, handler);
}
