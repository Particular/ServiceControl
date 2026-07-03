#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Text.Json;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
class RouteManifestEntrySerializationTests
{
    // The my/routes manifest must have ONE wire shape across instances. The Primary instance
    // serializes snake_case and the Monitoring instance camelCase, so RouteManifestEntry pins its
    // field names with [JsonPropertyName]. This test guards that contract: even under a camelCase
    // policy (as on the Monitoring host) the emitted names stay snake_case, so a client merging both
    // instances never silently drops the differently-cased entries.
    [Test]
    public void Emits_snake_case_field_names_even_under_a_camelCase_policy()
    {
        var json = JsonSerializer.Serialize(
            new RouteManifestEntry("GET", "/api/errors"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.That(json, Does.Contain("\"method\""));
        Assert.That(json, Does.Contain("\"url_template\""));
        Assert.That(json, Does.Not.Contain("urlTemplate"));
    }
}
