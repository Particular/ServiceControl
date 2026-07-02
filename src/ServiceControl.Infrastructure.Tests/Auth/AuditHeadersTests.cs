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
    public void Stamp_writes_id_and_name_headers()
    {
        var options = new SendOptions();
        AuditHeaders.Stamp(options, new AuditUser("alice-sub", "Alice"));

        var headers = options.GetHeaders();
        Assert.That(headers[AuditHeaders.SubjectId], Is.EqualTo("alice-sub"));
        Assert.That(headers[AuditHeaders.SubjectName], Is.EqualTo("Alice"));
    }

    [Test]
    public void Read_round_trips_stamped_identity()
    {
        var headers = new Dictionary<string, string>
        {
            [AuditHeaders.SubjectId] = "alice-sub",
            [AuditHeaders.SubjectName] = "Alice"
        };

        Assert.That(AuditHeaders.Read(headers), Is.EqualTo(new AuditUser("alice-sub", "Alice")));
    }

    [Test]
    public void Read_returns_anonymous_when_headers_absent()
    {
        Assert.That(AuditHeaders.Read(new Dictionary<string, string>()), Is.EqualTo(AuditUser.Anonymous));
    }
}
