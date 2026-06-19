#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Linq;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class PermissionTests
{
    [Test]
    public void Every_const_permission_round_trips_through_the_typed_model()
    {
        foreach (var value in Permissions.All)
        {
            Assert.That(PermissionId.TryParse(value, out var permission), Is.True, $"'{value}' did not parse.");
            Assert.That(permission.ToString(), Is.EqualTo(value), $"'{value}' did not round-trip.");
        }
    }

    [Test]
    public void Typed_catalogue_matches_the_const_catalogue()
    {
        var typed = PermissionId.All.Select(p => p.ToString());

        Assert.That(typed, Is.EquivalentTo(Permissions.All));
    }

    [Test]
    public void TryParse_rejects_a_well_typed_but_unknown_triple()
    {
        // audit:messages:retry uses valid segments but is not a real permission.
        Assert.That(PermissionId.TryParse("audit:messages:retry", out _), Is.False);
    }

    [Test]
    public void TryParse_rejects_malformed_input()
    {
        Assert.That(PermissionId.TryParse("error:messages", out _), Is.False);
        Assert.That(PermissionId.TryParse("error:messages:view:extra", out _), Is.False);
        Assert.That(PermissionId.TryParse("nope:nope:nope", out _), Is.False);
    }

    [Test]
    public void TryParse_is_case_insensitive()
    {
        Assert.That(PermissionId.TryParse("ERROR:Messages:VIEW", out var permission), Is.True);
        Assert.That(permission, Is.EqualTo(new PermissionId(InstanceId.Error, Component.Messages, AccessLevel.View)));
    }

    [Test]
    public void Pattern_matches_expected_permissions()
    {
        var viewPattern = PermissionPattern.Parse("*:*:view");

        Assert.That(viewPattern.Matches(PermissionId.Parse("error:messages:view")), Is.True);
        Assert.That(viewPattern.Matches(PermissionId.Parse("error:messages:retry")), Is.False);
    }
}
