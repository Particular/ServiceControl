#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class AuditUserTests
{
    [Test]
    public void Anonymous_has_sentinel_id_and_name()
    {
        Assert.That(AuditUser.Anonymous.Id, Is.EqualTo("anonymous"));
        Assert.That(AuditUser.Anonymous.Name, Is.EqualTo("anonymous"));
        Assert.That(AuditUser.AnonymousValue, Is.EqualTo("anonymous"));
    }
}
