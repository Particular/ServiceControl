#nullable enable
namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Mcp;
using ServiceControl.Mcp.Authorization;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.InternalMessages;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.Retrying.Metrics;
using ServiceControl.UnitTests.Operations;
using ServiceControl.UnitTests.Recoverability;

[TestFixture]
public class RetryToolsTests
{
    [Test]
    public async Task RetryFailedMessage_emits_an_audited_operation_and_message_headers()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var session = new TestableMessageSession();
        var audit = new RecordingMessageActionAuditLog();
        var tools = CreateTools(clock, session, new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock), audit);

        var result = await tools.RetryFailedMessage("msg-1");

        Assert.That(result.Status, Is.EqualTo("accepted"));
        Assert.That(result.Message, Does.Contain("msg-1"));

        var sent = session.SentMessages.Single(message => message.Message is RetryMessage);
        Assert.That(((RetryMessage)sent.Message).FailedMessageId, Is.EqualTo("msg-1"));

        var headers = sent.Options.GetHeaders();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(headers[AuditHeaders.SubjectId], Is.EqualTo("alice-sub-001"));
            Assert.That(headers[AuditHeaders.SubjectName], Is.EqualTo("Alice"));
            Assert.That(headers[AuditHeaders.OperationId], Is.EqualTo("trace-retry"));
        }

        var op = audit.Operations.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
            Assert.That(op.Permission, Is.EqualTo(Permissions.ErrorMessagesRetry));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Single));
            Assert.That(op.Resource, Is.EqualTo("msg-1"));
            Assert.That(op.OperationId, Is.EqualTo("trace-retry"));
            Assert.That(op.Success, Is.True);
        }
    }

    [Test]
    public async Task RetryFailureGroup_returns_in_progress_when_a_retry_is_already_running()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var session = new TestableMessageSession();
        var audit = new RecordingMessageActionAuditLog();
        var retryingManager = new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock);
        await retryingManager.Preparing("group-42", RetryType.FailureGroup, 10, clock.GetUtcNow().UtcDateTime);

        var tools = CreateTools(clock, session, retryingManager, audit);
        var result = await tools.RetryFailureGroup("group-42");

        Assert.That(result.Status, Is.EqualTo("in_progress"));
        Assert.That(session.SentMessages, Is.Empty);
        Assert.That(audit.Operations, Is.Empty);
    }

    [Test]
    public async Task RetryFailureGroup_emits_an_audited_operation_and_retry_message()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var session = new TestableMessageSession();
        var audit = new RecordingMessageActionAuditLog();
        var tools = CreateTools(clock, session, new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock), audit);

        var result = await tools.RetryFailureGroup("group-42");

        Assert.That(result.Status, Is.EqualTo("accepted"));
        Assert.That(result.Message, Does.Contain("group-42"));

        var sent = session.SentMessages.Single(message => message.Message is RetryAllInGroup);
        Assert.That(((RetryAllInGroup)sent.Message).GroupId, Is.EqualTo("group-42"));

        var op = audit.Operations.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(op.Kind, Is.EqualTo(MessageActionKind.Retry));
            Assert.That(op.Permission, Is.EqualTo(Permissions.ErrorRecoverabilityGroupsRetry));
            Assert.That(op.Scope, Is.EqualTo(MessageActionScope.Group));
            Assert.That(op.Resource, Is.EqualTo("group-42"));
            Assert.That(op.OperationId, Is.EqualTo("trace-retry"));
            Assert.That(op.Success, Is.True);
        }
    }

    static RetryTools CreateTools(TimeProvider clock, TestableMessageSession session, RetryingManager retryingManager, RecordingMessageActionAuditLog audit)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "trace-retry",
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "alice-sub-001"),
                    new Claim(ClaimTypes.Name, "Alice")
                }, authenticationType: "Bearer"))
            }
        };

        return new RetryTools(
            session,
            retryingManager,
            clock,
            new StubCurrentUserAccessor(new AuditUser("alice-sub-001", "Alice")),
            httpContextAccessor,
            audit,
            new McpAuthorizationService(new AllowAllAuthorizationService(), httpContextAccessor));
    }

    sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            throw new NotSupportedException();

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
    }
}