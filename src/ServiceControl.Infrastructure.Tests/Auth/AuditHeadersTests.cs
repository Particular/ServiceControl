#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Collections.Generic;
using NServiceBus;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class AuditHeadersTests
{
    [Test]
    public void Stamp_writes_id_name_and_operation_headers()
    {
        var options = new SendOptions();
        AuditHeaders.Stamp(options, new AuditUser("alice-sub", "Alice"), "op-123");

        var headers = options.GetHeaders();
        Assert.That(headers[AuditHeaders.SubjectId], Is.EqualTo("alice-sub"));
        Assert.That(headers[AuditHeaders.SubjectName], Is.EqualTo("Alice"));
        Assert.That(headers[AuditHeaders.OperationId], Is.EqualTo("op-123"));
    }

    [Test]
    public void Read_round_trips_stamped_identity_and_operation()
    {
        var headers = new Dictionary<string, string>
        {
            [AuditHeaders.SubjectId] = "alice-sub",
            [AuditHeaders.SubjectName] = "Alice",
            [AuditHeaders.OperationId] = "op-123"
        };

        var (user, operationId) = AuditHeaders.Read(headers);
        Assert.That(user, Is.EqualTo(new AuditUser("alice-sub", "Alice")));
        Assert.That(operationId, Is.EqualTo("op-123"));
    }

    [Test]
    public void Read_returns_anonymous_when_headers_absent()
    {
        var (user, operationId) = AuditHeaders.Read(new Dictionary<string, string>());
        Assert.That(user, Is.EqualTo(AuditUser.Anonymous));
        Assert.That(operationId, Is.Null);
    }
}
