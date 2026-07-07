#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Security.Claims;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class CurrentUserAccessorTests
{
    static CurrentUserAccessor Create()
    {
        // Default claim keys: SubjectIdClaim = "sub", SubjectNameClaim = "preferred_username".
        var settings = new OpenIdConnectSettings(new SettingsRootNamespace("ServiceControl"), validateConfiguration: false, requireServicePulseSettings: false);
        return new CurrentUserAccessor(settings);
    }

    static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Test]
    public void Resolves_id_and_name_from_configured_claims()
    {
        var user = Create().Resolve(Authenticated(new Claim("sub", "alice-sub"), new Claim("preferred_username", "Alice")));
        Assert.That(user.Id, Is.EqualTo("alice-sub"));
        Assert.That(user.Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void Falls_back_to_id_when_name_claim_missing()
    {
        var user = Create().Resolve(Authenticated(new Claim("sub", "alice-sub")));
        Assert.That(user.Name, Is.EqualTo("alice-sub"));
    }

    [Test]
    public void Anonymous_when_principal_is_null()
    {
        Assert.That(Create().Resolve(null), Is.EqualTo(AuditUser.Anonymous));
    }

    [Test]
    public void Anonymous_when_not_authenticated()
    {
        Assert.That(Create().Resolve(new ClaimsPrincipal(new ClaimsIdentity())), Is.EqualTo(AuditUser.Anonymous));
    }

    [Test]
    public void Anonymous_when_subject_claim_absent()
    {
        Assert.That(Create().Resolve(Authenticated(new Claim("preferred_username", "Alice"))), Is.EqualTo(AuditUser.Anonymous));
    }
}
