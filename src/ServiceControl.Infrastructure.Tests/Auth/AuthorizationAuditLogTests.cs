#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class AuthorizationAuditLogTests
{
    [Test]
    public void Decision_allow_emits_one_entry_on_audit_category()
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var auditLog = new AuthorizationAuditLog(factory);

        auditLog.Decision("alice-sub-001", "Alice Smith", "error:messages:retry", "acme.sales", allowed: true, reason: "role:reader matched");

        var entries = provider.EntriesFor("ServiceControl.Audit");
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Message, Does.Contain("alice-sub-001"));
        Assert.That(entries[0].Message, Does.Contain("Alice Smith"));
        Assert.That(entries[0].Message, Does.Contain("error:messages:retry"));
        Assert.That(entries[0].Message, Does.Contain("Allow:"));
        Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Information));
    }

    [Test]
    public void Decision_deny_emits_one_entry_on_audit_category()
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var auditLog = new AuthorizationAuditLog(factory);

        auditLog.Decision("bob-sub-002", "Bob Jones", "error:messages:retry", null, allowed: false, reason: "no matching role");

        var entries = provider.EntriesFor("ServiceControl.Audit");
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Message, Does.Contain("bob-sub-002"));
        Assert.That(entries[0].Message, Does.Contain("Bob Jones"));
        Assert.That(entries[0].Message, Does.Contain("error:messages:retry"));
        Assert.That(entries[0].Message, Does.Contain("Deny:"));
        Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void Decision_does_not_appear_on_other_categories()
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var auditLog = new AuthorizationAuditLog(factory);

        auditLog.Decision("carol-sub-003", "Carol White", "error:endpoints:view", null, allowed: true, reason: "role:reader matched");

        Assert.That(provider.EntriesFor("ServiceControl.SomeOtherCategory"), Is.Empty);
    }

    [Test]
    public void Multiple_decisions_accumulate_in_order()
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var auditLog = new AuthorizationAuditLog(factory);

        auditLog.Decision("alice-sub-001", "alice", "error:messages:view", null, allowed: true, "role matched");
        auditLog.Decision("alice-sub-001", "alice", "error:messages:retry", "acme.finance", allowed: false, "out of scope");

        var entries = provider.EntriesFor("ServiceControl.Audit");
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].Message, Does.Contain("Allow:"));
        Assert.That(entries[1].Message, Does.Contain("Deny:"));
    }

    [TestCase(null, "Alice", "error:messages:retry", "reason")]
    [TestCase("", "Alice", "error:messages:retry", "reason")]
    [TestCase("alice-sub-001", null, "error:messages:retry", "reason")]
    [TestCase("alice-sub-001", "", "error:messages:retry", "reason")]
    [TestCase("alice-sub-001", "Alice", null, "reason")]
    [TestCase("alice-sub-001", "Alice", "", "reason")]
    [TestCase("alice-sub-001", "Alice", "error:messages:retry", null)]
    [TestCase("alice-sub-001", "Alice", "error:messages:retry", "")]
    public void Decision_throws_when_required_argument_is_null_or_empty(string? subjectId, string? subjectName, string? permission, string? reason)
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var auditLog = new AuthorizationAuditLog(factory);

        Assert.That(
            () => auditLog.Decision(subjectId!, subjectName!, permission!, resource: null, allowed: true, reason: reason!),
            Throws.InstanceOf<ArgumentException>());
    }
}
