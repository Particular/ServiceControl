#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Collections.Generic;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
class RouteManifestFilterTests
{
    static readonly RouteAuthInfo Retry = new("POST", "/api/errors/{id}/retry", "error:messages:retry", false);
    static readonly RouteAuthInfo Archive = new("POST", "/api/errors/{id}/archive", "error:messages:archive", false);
    static readonly RouteAuthInfo Configuration = new("GET", "/api/configuration", null, false);
    static readonly RouteAuthInfo Root = new("GET", "/api", null, true);

    [Test]
    public void Includes_granted_permissioned_anonymous_and_authenticated_only_routes()
    {
        var routes = new[] { Retry, Archive, Configuration, Root };
        var effective = new HashSet<string> { "error:messages:retry" };

        var result = RouteManifestFilter.Filter(routes, effective);

        Assert.That(result, Is.EquivalentTo(new[]
        {
            new RouteManifestEntry("POST", "/api/errors/{id}/retry"),
            new RouteManifestEntry("GET", "/api/configuration"),
            new RouteManifestEntry("GET", "/api"),
        }));
    }

    [Test]
    public void Excludes_permissioned_routes_not_in_the_effective_set()
    {
        var result = RouteManifestFilter.Filter(new[] { Archive }, new HashSet<string>());

        Assert.That(result, Is.Empty);
    }
}
