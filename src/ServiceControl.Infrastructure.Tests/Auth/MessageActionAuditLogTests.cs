#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class MessageActionAuditLogTests
{
    static (RecordingLoggerProvider provider, MessageActionAuditLog log) Create()
    {
        var provider = new RecordingLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(provider));
        return (provider, new MessageActionAuditLog(factory));
    }

    [Test]
    public void Operation_emits_one_entry_on_operation_category()
    {
        var (provider, log) = Create();

        log.Operation(new AuditUser("alice-sub", "Alice"), MessageActionKind.Retry,
            "error:recoverabilitygroups:retry", MessageActionScope.Group, resource: "group-1", count: 42, operationId: "op-1");

        var entries = provider.EntriesFor("ServiceControl.Audit");
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Information));
        var ecs = JsonDocument.Parse(entries[0].Message).RootElement;
        Assert.That(ecs.GetProperty("event").GetProperty("category")[0].GetString(), Is.EqualTo("configuration"));
        Assert.That(ecs.GetProperty("event").GetProperty("type")[0].GetString(), Is.EqualTo("change"));
        Assert.That(ecs.GetProperty("event").GetProperty("action").GetString(), Is.EqualTo("error:recoverabilitygroups:retry"));
        Assert.That(ecs.GetProperty("event").GetProperty("outcome").GetString(), Is.EqualTo("success"));
        Assert.That(ecs.GetProperty("user").GetProperty("id").GetString(), Is.EqualTo("alice-sub"));
        Assert.That(ecs.GetProperty("servicecontrol").GetProperty("scope").GetString(), Is.EqualTo("group"));
        Assert.That(ecs.GetProperty("servicecontrol").GetProperty("resource").GetString(), Is.EqualTo("group-1"));
        Assert.That(ecs.GetProperty("servicecontrol").GetProperty("count").GetInt32(), Is.EqualTo(42));
        Assert.That(ecs.GetProperty("servicecontrol").GetProperty("operation").GetProperty("id").GetString(), Is.EqualTo("op-1"));
    }

    [Test]
    public void Archive_maps_to_deletion_event_type()
    {
        var (provider, log) = Create();

        log.Operation(AuditUser.Anonymous, MessageActionKind.Archive,
            "error:messages:archive", MessageActionScope.Single, resource: "m-1", count: 1, operationId: "op-2");

        var ecs = JsonDocument.Parse(provider.EntriesFor("ServiceControl.Audit")[0].Message).RootElement;
        Assert.That(ecs.GetProperty("event").GetProperty("type")[0].GetString(), Is.EqualTo("deletion"));
        Assert.That(ecs.GetProperty("user").GetProperty("id").GetString(), Is.EqualTo("anonymous"));
    }

    [Test]
    public void MessageAction_emits_on_messages_subcategory_with_event_id_2002()
    {
        var (provider, log) = Create();

        log.MessageAction(new AuditUser("bob-sub", "Bob"), MessageActionKind.Unarchive,
            "error:messages:unarchive", MessageActionScope.Batch, messageId: "m-9", operationId: "op-3");

        Assert.That(provider.EntriesFor("ServiceControl.Audit"), Is.Empty);
        var entries = provider.EntriesFor("ServiceControl.Audit.Messages");
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].EventId.Id, Is.EqualTo(2002));
        var ecs = JsonDocument.Parse(entries[0].Message).RootElement;
        Assert.That(ecs.GetProperty("servicecontrol").GetProperty("message").GetProperty("id").GetString(), Is.EqualTo("m-9"));
        Assert.That(ecs.GetProperty("event").GetProperty("type")[0].GetString(), Is.EqualTo("change"));
    }

    [Test]
    public void Operation_failure_logs_as_warning()
    {
        var (provider, log) = Create();

        log.Operation(new AuditUser("a", "a"), MessageActionKind.Retry, "error:messages:retry",
            MessageActionScope.All, resource: null, count: null, operationId: "op-4", success: false);

        var entry = provider.EntriesFor("ServiceControl.Audit")[0];
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry.EventId.Id, Is.EqualTo(2001));
        var ecs = JsonDocument.Parse(entry.Message).RootElement;
        Assert.That(ecs.GetProperty("event").GetProperty("outcome").GetString(), Is.EqualTo("failure"));
    }

    [Test]
    public void Null_valued_fields_are_omitted()
    {
        var (provider, log) = Create();

        log.Operation(new AuditUser("a", "a"), MessageActionKind.Retry, "error:messages:retry",
            MessageActionScope.All, resource: null, count: null, operationId: "op-5");

        var sc = JsonDocument.Parse(provider.EntriesFor("ServiceControl.Audit")[0].Message).RootElement.GetProperty("servicecontrol");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sc.TryGetProperty("resource", out _), Is.False);
            Assert.That(sc.TryGetProperty("count", out _), Is.False);
            Assert.That(sc.TryGetProperty("message", out _), Is.False);
            Assert.That(sc.GetProperty("operation").GetProperty("id").GetString(), Is.EqualTo("op-5"));
        }
    }

    [TestCase(null, "op")]
    [TestCase("", "op")]
    [TestCase("error:messages:retry", null)]
    [TestCase("error:messages:retry", "")]
    public void Operation_throws_when_permission_or_operationId_missing(string? permission, string? operationId)
    {
        var (_, log) = Create();
        Assert.That(
            () => log.Operation(AuditUser.Anonymous, MessageActionKind.Retry, permission!, MessageActionScope.All, null, null, operationId!),
            Throws.InstanceOf<System.ArgumentException>());
    }
}
