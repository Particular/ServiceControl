#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Shared helpers for the S2 authorization mechanism.
/// Centralises patterns used across controllers and the resource-scope checker.
/// </summary>
public static class AuthorizationHelpers
{
    /// <summary>
    /// Extracts a human-readable subject identifier from the principal.
    /// Prefers the <c>sub</c> claim; falls back to <see cref="System.Security.Principal.IIdentity.Name"/>; then "unknown".
    /// </summary>
    public static string GetSubject(ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value
        ?? user.Identity?.Name
        ?? "unknown";

    /// <summary>
    /// Writes a structured JSON 403 body to the HTTP response.
    /// Use this whenever a resource-scope check denies access to a single resource,
    /// then return <c>Empty</c> from the controller action.
    /// </summary>
    /// <param name="response">The current <see cref="HttpResponse"/>.</param>
    /// <param name="permission">The permission that was evaluated.</param>
    /// <param name="queueAddress">The queue address of the resource, or <see langword="null"/> if unknown.</param>
    public static async Task WriteScopeDenied403(HttpResponse response, string permission, string? queueAddress)
    {
        response.ContentType = "application/json";
        response.StatusCode = StatusCodes.Status403Forbidden;
        await response.WriteAsJsonAsync(new
        {
            error = "forbidden",
            permission,
            resource = queueAddress,
            reason = string.IsNullOrEmpty(queueAddress)
                ? "Message has no resolvable queue address"
                : $"Queue '{queueAddress}' is out of scope for permission '{permission}'"
        });
    }
}
