#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Security.Claims;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class RolesClaimExtractorTests
{
    [Test]
    public void Flat_claim_with_repeated_string_values_returns_each_value()
    {
        var principal = PrincipalWith(
            new Claim("roles", "operator"),
            new Claim("roles", "viewer"));

        var result = RolesClaimExtractor.Extract(principal, "roles");

        Assert.That(result, Is.EquivalentTo(new[] { "operator", "viewer" }));
    }

    [Test]
    public void Flat_claim_serialized_as_json_array_string_is_decoded()
    {
        var principal = PrincipalWith(new Claim("roles", "[\"admin\",\"writer\"]"));

        var result = RolesClaimExtractor.Extract(principal, "roles");

        Assert.That(result, Is.EquivalentTo(new[] { "admin", "writer" }));
    }

    [Test]
    public void Nested_keycloak_path_extracts_realm_access_roles()
    {
        var principal = PrincipalWith(new Claim(
            "realm_access",
            "{\"roles\":[\"sc-admin\",\"sc-operator\"]}"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.roles");

        Assert.That(result, Is.EquivalentTo(new[] { "sc-admin", "sc-operator" }));
    }

    [Test]
    public void Nested_path_with_single_string_value_returns_one_value()
    {
        var principal = PrincipalWith(new Claim(
            "realm_access",
            "{\"role\":\"sc-admin\"}"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.role");

        Assert.That(result, Is.EqualTo(new[] { "sc-admin" }));
    }

    [Test]
    public void Missing_top_level_claim_returns_empty()
    {
        var principal = PrincipalWith(new Claim("other", "anything"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.roles");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Missing_nested_property_returns_empty()
    {
        var principal = PrincipalWith(new Claim(
            "realm_access",
            "{\"resource_access\":{}}"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.roles");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Malformed_json_in_nested_claim_returns_empty()
    {
        var principal = PrincipalWith(new Claim("realm_access", "not json"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.roles");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Empty_or_whitespace_path_returns_empty()
    {
        var principal = PrincipalWith(new Claim("roles", "viewer"));

        Assert.That(RolesClaimExtractor.Extract(principal, ""), Is.Empty);
        Assert.That(RolesClaimExtractor.Extract(principal, "   "), Is.Empty);
    }

    [Test]
    public void Non_string_array_entries_are_skipped()
    {
        var principal = PrincipalWith(new Claim(
            "realm_access",
            "{\"roles\":[\"valid\",42,null,\"alsovalid\"]}"));

        var result = RolesClaimExtractor.Extract(principal, "realm_access.roles");

        Assert.That(result, Is.EquivalentTo(new[] { "valid", "alsovalid" }));
    }

    [Test]
    public void Multiple_top_level_claims_with_dotted_path_aggregate_values()
    {
        var principal = PrincipalWith(
            new Claim("resource_access", "{\"client-a\":{\"roles\":[\"role-a\"]}}"),
            new Claim("resource_access", "{\"client-a\":{\"roles\":[\"role-b\"]}}"));

        var result = RolesClaimExtractor.Extract(principal, "resource_access.client-a.roles");

        Assert.That(result, Is.EquivalentTo(new[] { "role-a", "role-b" }));
    }

    static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
