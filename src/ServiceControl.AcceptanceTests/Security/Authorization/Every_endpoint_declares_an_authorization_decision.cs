namespace ServiceControl.AcceptanceTests.Security.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.WebApi;

    /// <summary>
    /// Asserts that every API endpoint in the ServiceControl host carries an explicit
    /// authorization decision — either:
    /// <list type="bullet">
    ///   <item><see cref="RequirePermissionAttribute"/> — a specific permission is required (Phase 1+)</item>
    ///   <item><see cref="AuthenticatedOnlyAttribute"/> — reviewed: authenticated but no permission needed</item>
    ///   <item><see cref="AllowAnonymousAttribute"/> — reviewed: public (e.g. health/metadata endpoints)</item>
    /// </list>
    ///
    /// <para>In Phase 0, no endpoints have <see cref="RequirePermissionAttribute"/> yet, so the entire
    /// set is in the <see cref="Phase0Baseline"/>.  As Phase 1+ wires permissions the baseline shrinks.
    /// Any endpoint NOT in the baseline and NOT carrying one of the three attributes fails the test
    /// immediately, catching new endpoints that were added without a decision.</para>
    /// </summary>
    class Every_endpoint_declares_an_authorization_decision : AcceptanceTest
    {
        /// <summary>
        /// The Phase 0 baseline: all endpoints that do not yet have a permission declaration.
        /// A route in this set is allowed to be "uncovered" — it was here before RBAC.
        /// Remove routes from this list as Phase 1+ wires <see cref="RequirePermissionAttribute"/>;
        /// this set reaching empty is the definition of "coverage complete".
        /// </summary>
        static readonly HashSet<string> Phase0Baseline =
        [
            // Authentication (AllowAnonymous — will never be in the baseline)
            // These are here so the test can document what endpoints exist during Phase 0.
            // Endpoints below lack [RequirePermission] / [AuthenticatedOnly] / [AllowAnonymous]:

            // Message failures area — wired in Phase 1
            // POST api/errors/{failedMessageId}/retry — enforced on s2 (removed from baseline)
            // POST api/errors/retry — enforced on s2 (removed from baseline)
            // POST api/errors/retry/all — enforced on s2 (removed from baseline)
            // POST api/errors/queues/{queueAddress}/retry — enforced on s2 (removed from baseline)
            // POST api/errors/{endpointName}/retry/all — enforced on s2 (removed from baseline)
            "GET api/errors",
            "HEAD api/errors",
            "GET api/errors/summary",
            "GET api/errors/{failedMessageId:required:minlength(1)}",
            "GET api/errors/last/{failedMessageId:required:minlength(1)}",
            "PATCH/POST api/errors/{messageId:required:minlength(1)}/archive",
            "PATCH/POST api/errors/archive",
            "PATCH api/errors/unarchive",
            "PATCH api/errors/{from}...{to}/unarchive",
            "GET api/errors/groups/{classifier?}",
            "GET api/archive/groups/id/{groupId:required:minlength(1)}",

            // Edit area — wired in Phase 1
            "GET api/edit/config",
            "POST api/edit/{failedMessageId:required:minlength(1)}",

            // Pending retries area — wired in Phase 1
            "GET api/pendingretries",
            "GET api/pendingretries/{queue}",
            "POST api/pendingretries/retry",
            "POST api/pendingretries/queues/retry",
            "PATCH api/pendingretries/resolve",
            "PATCH api/pendingretries/queues/resolve",

            // Queue addresses — wired later
            "GET api/errors/queues/addresses",
            "GET api/errors/queues/addresses/search/{search}",
            "DELETE api/errors/queues/{queueaddress}",

            // Recoverability area — wired in Phase 1
            "GET api/recoverability/groups/{classifier?}",
            "GET api/recoverability/groups/{groupId:required:minlength(1)}/errors",
            "HEAD api/recoverability/groups/{groupId:required:minlength(1)}/errors",
            "POST api/recoverability/groups/{groupId:required:minlength(1)}/comment",
            "DELETE api/recoverability/groups/{groupId:required:minlength(1)}/comment",
            "GET api/recoverability/groups/id/{groupId:required:minlength(1)}",
            "POST api/recoverability/groups/{groupId:required:minlength(1)}/errors/retry",
            "POST api/recoverability/groups/{groupId:required:minlength(1)}/errors/archive",
            "POST api/recoverability/groups/{groupId:required:minlength(1)}/errors/unarchive",
            "DELETE api/recoverability/unacknowledgedgroups/{groupId:required:minlength(1)}",
            "GET api/recoverability/classifiers",
            "GET api/recoverability/history",

            // Messages search area — wired in Phase 1
            "GET api/messages",
            "GET api/messages2",
            "GET api/messages/search/{keyword}",
            "GET api/messages/search",
            "GET api/messages/{id}/body",
            "GET api/conversations/{conversationId:required:minlength(1)}",
            "GET api/endpoints/{endpoint}/messages",
            "GET api/endpoints/{endpoint}/messages/search",
            "GET api/endpoints/{endpoint}/messages/search/{keyword}",
            "GET api/endpoints/{endpoint}/audit-count",

            // Endpoints monitoring area — wired later
            "GET api/endpoints",
            "GET api/endpoints/{id}",
            "GET api/endpoints/known",
            "GET api/endpoints/{endpointname}/errors",
            "GET api/heartbeatstatus",
            "GET api/heartbeats/stats",
            "PATCH api/endpoints/{endpointId}",
            "DELETE api/endpoints/{endpointId}",

            // Endpoint settings — wired later
            "GET api/endpointssettings",
            "PATCH api/endpointssettings/{endpointName?}",

            // Custom checks — wired later
            "GET api/customchecks",
            "DELETE api/customchecks/{id}",

            // Sagas — wired later
            "GET api/sagas/{id}",

            // Event log — wired later
            "GET api/eventlogitems",

            // Licensing — wired later
            "GET api/license",
            "POST api/license",
            "GET api/licensing/endpoints",
            "POST api/licensing/endpoints/update",
            "GET api/licensing/report/available",
            "GET api/licensing/report/file",
            "GET api/licensing/settings/info",
            "GET api/licensing/settings/test",
            "GET api/licensing/settings/masks",
            "POST api/licensing/settings/masks/update",

            // Notifications — wired later
            "GET api/notifications",
            "GET api/notifications/email",
            "POST api/notifications/email",
            "POST api/notifications/email/toggle",
            "POST api/notifications/email/test",
            "DELETE api/notifications",

            // Message redirects — wired later
            "GET api/redirects",
            "POST api/redirects",
            "HEAD api/redirect",
            "PUT api/redirects/{messageRedirectId:guid}",
            "DELETE api/redirects/{messageRedirectId:guid}",

            // Connections — wired later
            "GET api/connection",

            // Failed errors / failed message retries — wired later
            "GET api/failederrors/count",
            "POST api/failederrors/import",
            "GET api/failedmessageretries/count",

            // Internal / test endpoints — wired later
            "GET api/test/knownendpoints/query",
            "POST api/criticalerror/trigger",

            // Root (AllowAnonymous) — documented but not in baseline (they already pass)
            // "GET api"
            // "GET api/instance-info"
            // "GET api/configuration"
            // "GET api/configuration/remotes"
        ];

        [Test]
        public async Task All_endpoints_have_a_reviewed_authorization_decision()
        {
            var uncoveredEndpoints = new List<string>();
            var newUncoveredEndpoints = new List<string>(); // NEW endpoints without a decision — always fail

            // Enumerate endpoints inside Done() while the host (and its IServiceProvider) is still alive.
            // Accessing dataSource.Endpoints after Run() completes is unsafe when the authorization
            // policy provider is registered — ASP.NET Core's RouteEndpointDataSource resolves
            // endpoint metadata lazily via the service provider, which is disposed after Run().
            _ = await Define<Context>()
                .Done(ctx =>
                {
                    var dataSources = Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

                    foreach (var dataSource in dataSources)
                    {
                        foreach (var endpoint in dataSource.Endpoints)
                        {
                            // Only check MVC controller actions
                            var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                            if (actionDescriptor == null)
                            {
                                continue;
                            }

                            // Build a route key: "METHOD path/template"
                            var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unknown";
                            var httpMethods = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()
                                .SelectMany(m => m.HttpMethods)
                                .Distinct()
                                .OrderBy(x => x)
                                .ToList();

                            var httpMethod = httpMethods.Count == 1 ? httpMethods[0] : string.Join("/", httpMethods);
                            var routeKey = $"{httpMethod} {routePattern}";

                            // Check for one of the three valid authorization decisions
                            if (HasAuthorizationDecision(endpoint, actionDescriptor))
                            {
                                continue; // Covered — has a decision
                            }

                            if (Phase0Baseline.Contains(routeKey))
                            {
                                uncoveredEndpoints.Add(routeKey); // Known uncovered — in baseline, Phase 1+ will fix
                            }
                            else
                            {
                                newUncoveredEndpoints.Add(routeKey); // NEW endpoint without decision — fail now
                            }
                        }
                    }

                    return Task.FromResult(true);
                })
                .Run();

            if (newUncoveredEndpoints.Count > 0)
            {
                Assert.Fail(
                    $"The following endpoints were added without an authorization decision. " +
                    $"Add [RequirePermission], [AuthenticatedOnly], or [AllowAnonymous] to each:\n" +
                    string.Join("\n", newUncoveredEndpoints.Select(e => $"  - {e}")));
            }

            // Log the remaining baseline for visibility (informational, not a failure in Phase 0)
            if (uncoveredEndpoints.Count > 0)
            {
                TestContext.Out.WriteLine(
                    $"[Phase 0 baseline] {uncoveredEndpoints.Count} endpoint(s) still need authorization wiring:\n" +
                    string.Join("\n", uncoveredEndpoints.Select(e => $"  - {e}")));
            }
        }

        /// <summary>
        /// Returns true if the endpoint has one of the three recognized authorization decisions.
        /// Checks both class-level and method-level attributes (method-level takes precedence in MVC).
        /// </summary>
        static bool HasAuthorizationDecision(Microsoft.AspNetCore.Http.Endpoint endpoint, ControllerActionDescriptor descriptor)
        {
            var controllerType = descriptor.ControllerTypeInfo;
            var methodInfo = descriptor.MethodInfo;

            return
                // RequirePermissionAttribute — Phase 1+ enforcement
                endpoint.Metadata.GetMetadata<RequirePermissionAttribute>() != null ||
                controllerType.GetCustomAttribute<RequirePermissionAttribute>() != null ||
                methodInfo.GetCustomAttribute<RequirePermissionAttribute>() != null ||

                // AuthenticatedOnlyAttribute — reviewed: no specific permission needed
                endpoint.Metadata.GetMetadata<AuthenticatedOnlyAttribute>() != null ||
                controllerType.GetCustomAttribute<AuthenticatedOnlyAttribute>() != null ||
                methodInfo.GetCustomAttribute<AuthenticatedOnlyAttribute>() != null ||

                // AllowAnonymousAttribute — reviewed: public endpoint
                endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() != null ||
                controllerType.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                methodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null;
        }

        class Context : ScenarioContext;
    }
}
