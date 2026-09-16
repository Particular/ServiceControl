#nullable enable
namespace ServiceControl.Mcp.Authorization;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Wraps the existing ServiceControl authorization pipeline so MCP tools can enforce the exact same
/// permission policies as the built-in HTTP API.
/// </summary>
public sealed class McpAuthorizationService(
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task RequirePermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(permission);

        var user = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        var result = await authorizationService.AuthorizeAsync(user, resource: null, permission);

        if (result.Succeeded)
        {
            return;
        }

        throw new UnauthorizedAccessException($"Access denied for permission '{permission}'.");
    }
}