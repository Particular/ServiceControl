#nullable enable
namespace ServiceControl.UnitTests.Mcp;

using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Mcp.Authorization;

[TestFixture]
public class McpPermissionsTests
{
    [Test]
    public void Failure_and_retry_tools_reuse_the_existing_permission_constants()
    {
        Assert.That(McpPermissions.ListFailures, Is.EqualTo(Permissions.ErrorMessagesView));
        Assert.That(McpPermissions.GetFailure, Is.EqualTo(Permissions.ErrorMessagesView));
        Assert.That(McpPermissions.RetryFailure, Is.EqualTo(Permissions.ErrorMessagesRetry));
        Assert.That(McpPermissions.ListFailureGroups, Is.EqualTo(Permissions.ErrorRecoverabilityGroupsView));
        Assert.That(McpPermissions.GetFailureGroup, Is.EqualTo(Permissions.ErrorRecoverabilityGroupsView));
        Assert.That(McpPermissions.RetryFailureGroup, Is.EqualTo(Permissions.ErrorRecoverabilityGroupsRetry));
    }
}