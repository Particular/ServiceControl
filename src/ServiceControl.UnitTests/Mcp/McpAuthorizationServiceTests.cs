#nullable enable
namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using ServiceControl.Mcp.Authorization;

[TestFixture]
public class McpAuthorizationServiceTests
{
    [Test]
    public async Task RequirePermissionAsync_uses_the_current_http_user_and_permission_name()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "alice-sub-001"),
                    new Claim(ClaimTypes.Name, "Alice Smith"),
                    new Claim(ClaimTypes.Role, "reader")
                }, authenticationType: "Bearer"))
            }
        };

        var authorizationService = new RecordingAuthorizationService();
        var service = new McpAuthorizationService(authorizationService, httpContextAccessor);

        await service.RequirePermissionAsync(McpPermissions.RetryFailure, CancellationToken.None);

        Assert.That(authorizationService.Permission, Is.EqualTo(McpPermissions.RetryFailure));
        Assert.That(authorizationService.User?.Identity?.IsAuthenticated, Is.True);
        Assert.That(authorizationService.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, Is.EqualTo("alice-sub-001"));
        Assert.That(authorizationService.User?.FindFirst(ClaimTypes.Role)?.Value, Is.EqualTo("reader"));
    }

    [Test]
    public void RequirePermissionAsync_throws_when_authorization_fails()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var authorizationService = new DenyingAuthorizationService();
        var service = new McpAuthorizationService(authorizationService, httpContextAccessor);

        Assert.That(
            async () => await service.RequirePermissionAsync(McpPermissions.GetFailureGroup, CancellationToken.None),
            Throws.TypeOf<UnauthorizedAccessException>().With.Message.Contains(McpPermissions.GetFailureGroup));
    }

    sealed class RecordingAuthorizationService : IAuthorizationService
    {
        public ClaimsPrincipal? User { get; private set; }
        public string? Permission { get; private set; }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            throw new System.NotSupportedException();
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            User = user;
            Permission = policyName;
            return Task.FromResult(AuthorizationResult.Success());
        }
    }

    sealed class DenyingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            throw new System.NotSupportedException();
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}