namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using NUnit.Framework;
using ServiceControl.Hosting.Auth;

[TestFixture]
public class RealmAccessClaimsTransformationTests
{
    [Test]
    public async Task Flattens_realm_access_roles_into_role_claims()
    {
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("realm_access", """{"roles":["sc-admin","sc-operator"]}""", JsonClaimValueTypes.Json));
        var principal = new ClaimsPrincipal(identity);

        var result = await new RealmAccessClaimsTransformation().TransformAsync(principal);

        var roles = result.FindAll("role").Select(c => c.Value).ToArray();
        Assert.That(roles, Is.EquivalentTo(new[] { "sc-admin", "sc-operator" }));
    }

    [Test]
    public async Task Does_not_duplicate_role_claims_on_repeated_transformation()
    {
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("realm_access", """{"roles":["sc-admin"]}""", JsonClaimValueTypes.Json));
        var principal = new ClaimsPrincipal(identity);

        var transformation = new RealmAccessClaimsTransformation();
        var result = await transformation.TransformAsync(principal);
        result = await transformation.TransformAsync(result);

        var roles = result.FindAll("role").Select(c => c.Value).ToArray();
        Assert.That(roles, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Principal_without_realm_access_is_returned_unchanged()
    {
        var identity = new ClaimsIdentity("Bearer");
        identity.AddClaim(new Claim("sub", "user123"));
        var principal = new ClaimsPrincipal(identity);

        var result = await new RealmAccessClaimsTransformation().TransformAsync(principal);

        Assert.That(result.FindAll("role"), Is.Empty);
    }
}
