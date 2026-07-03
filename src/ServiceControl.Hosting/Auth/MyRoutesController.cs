#nullable enable
namespace ServiceControl.Hosting.Auth;

using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Returns the API routes the current token may call, as <c>{ method, url_template }</c> entries.
/// This is the per-instance authorization contract for clients (ServicePulse): each instance reports
/// only the routes it serves, so a client matches its outgoing request against the allowed set without
/// ever learning the server's internal permission vocabulary. The endpoint is the bootstrap of that
/// contract, so it is reachable by any authenticated user ([Authorize], no specific permission).
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class MyRoutesController(RouteAuthorizationTable table, OpenIdConnectSettings settings) : ControllerBase
{
    [HttpGet]
    [Route("my/routes")]
    public ActionResult<IReadOnlyList<RouteManifestEntry>> GetMyRoutes()
    {
        var effective = EffectivePermissions.ForUser(User, settings);
        return Ok(RouteManifestFilter.Filter(table.Entries, effective));
    }
}
