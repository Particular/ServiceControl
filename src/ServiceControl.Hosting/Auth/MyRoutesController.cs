#nullable enable
namespace ServiceControl.Hosting.Auth;

using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Returns the caller's role claims and the API routes the current token may call, as
/// <c>{ method, url_template }</c> entries. This is the per-instance authorization contract for
/// clients (ServicePulse): each instance reports only the routes it serves, so a client matches its
/// outgoing request against the allowed set without ever learning the server's internal permission
/// vocabulary. The endpoint is the bootstrap of that contract, so it is reachable by any authenticated
/// user ([Authorize], no specific permission).
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class MyRoutesController(RouteAuthorizationTable table, OpenIdConnectSettings settings) : ControllerBase
{
    [HttpGet]
    [Route("my/routes")]
    public ActionResult<MyRoutesResponse> GetMyRoutes()
    {
        var effective = EffectivePermissions.ForUser(User, settings);
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray();
        return Ok(new MyRoutesResponse(roles, RouteManifestFilter.Filter(table.Entries, effective)));
    }
}
