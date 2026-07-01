#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System.Security.Claims;
using ServiceControl.Infrastructure.Auth;

sealed class StubCurrentUserAccessor(AuditUser user) : ICurrentUserAccessor
{
    public AuditUser Resolve(ClaimsPrincipal? principal) => user;
}
